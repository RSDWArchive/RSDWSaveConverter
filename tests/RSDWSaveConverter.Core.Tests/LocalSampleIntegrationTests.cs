using System.Security.Cryptography;
using RSDWSaveConverter.Core.Wgs;

namespace RSDWSaveConverter.Core.Tests;

public sealed class LocalSampleIntegrationTests
{
    private static readonly string SampleRoot =
        Environment.GetEnvironmentVariable("RSDW_SAVE_SAMPLE_ROOT")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Sample");

    private static readonly string CharacterSavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RSDragonwilds",
        "Saved",
        "SaveCharacters",
        "Beb.json");

    [Fact]
    [Trait("Category", "LocalFixture")]
    public void CapturedIndexesRoundTripByteForByte()
    {
        if (!Directory.Exists(SampleRoot))
        {
            return;
        }

        foreach (var snapshot in new[] { "Before", "After" })
        {
            var path = Path.Combine(SampleRoot, snapshot, WgsIndex.FileName);
            var original = File.ReadAllBytes(path);
            var rewritten = WgsIndex.Load(path).ToBytes();
            Assert.Equal(original, rewritten);
        }
    }

    [Fact]
    [Trait("Category", "LocalFixture")]
    public void CapturedWorldPayloadsRoundTripThroughDotNetZlib()
    {
        if (!Directory.Exists(SampleRoot))
        {
            return;
        }

        foreach (var snapshot in new[] { "Before", "After" })
        {
            var profile = WgsProfile.Load(Path.Combine(SampleRoot, snapshot));
            var testWorld = profile.WorldSlots.Single(slot => slot.SlotName == "TestWorld1");
            var payload = WgsProfile.ReadPayload(profile.Path, testWorld.ActiveEntry);
            var raw = DragonwildsSaveCodec.Unwrap(payload);
            var dotNetWrapped = DragonwildsSaveCodec.Wrap(raw);
            Assert.Equal(raw, DragonwildsSaveCodec.Unwrap(dotNetWrapped));
            Assert.Equal(0x78, dotNetWrapped[12]);
            Assert.Equal(0x9C, dotNetWrapped[13]);
        }
    }

    [Fact]
    [Trait("Category", "LocalFixture")]
    public void LooterParadiseMetadataIsReadable()
    {
        var savePath = Path.Combine(SampleRoot, "LP3", "Looter Paradise3.sav");
        if (!File.Exists(savePath))
        {
            return;
        }

        var raw = DragonwildsSaveCodec.ReadRawSave(savePath);
        var metadata = DragonwildsSaveCodec.ReadMetadata(raw, Path.GetFileNameWithoutExtension(savePath));

        Assert.Equal("Looter Paradise3", metadata.WorldName);
        Assert.Equal(raw.Length, metadata.UncompressedSize);
        Assert.Equal("00753DDA3E7423C3291B23392031582940C294C028245DC11F41E7FB984483DB", Hash(raw));
    }

    [Fact]
    [Trait("Category", "LocalFixture")]
    public void ImportReplacesWorldInTemporaryProfileCopy()
    {
        var sourceProfile = Path.Combine(SampleRoot, "After");
        var savePath = Path.Combine(SampleRoot, "LP3", "Looter Paradise3.sav");
        if (!Directory.Exists(sourceProfile) || !File.Exists(savePath))
        {
            return;
        }

        var testRoot = Path.Combine(Path.GetTempPath(), "RSDWSaveConverter.Tests", Guid.NewGuid().ToString("N"));
        var profileCopy = Path.Combine(testRoot, "Profile");
        var backups = Path.Combine(testRoot, "Backups");
        Directory.CreateDirectory(profileCopy);
        CopyDirectory(sourceProfile, profileCopy);

        try
        {
            var beforeSourceHash = Hash(File.ReadAllBytes(Path.Combine(sourceProfile, WgsIndex.FileName)));
            var raw = DragonwildsSaveCodec.ReadRawSave(savePath);
            var importer = new WgsImporter();
            var result = importer.ReplaceWorld(profileCopy, "TestWorld1Qxav", raw, backups);

            Assert.Equal("Looter Paradise3", result.SaveName);
            Assert.True(File.Exists(result.BackupPath));

            var imported = WgsProfile.Load(profileCopy);
            var slot = imported.WorldSlots.Single(world => world.SlotName == "TestWorld1");
            var installedRaw = DragonwildsSaveCodec.Unwrap(WgsProfile.ReadPayload(profileCopy, slot.ActiveEntry));
            Assert.Equal(raw, installedRaw);
            Assert.Equal(beforeSourceHash, Hash(File.ReadAllBytes(Path.Combine(sourceProfile, WgsIndex.FileName))));
        }
        finally
        {
            var resolved = Path.GetFullPath(testRoot);
            var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "RSDWSaveConverter.Tests"));
            if (resolved.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
            {
                Directory.Delete(resolved, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "LocalFixture")]
    public void CapturedProfileDiscoversCharacterPayload()
    {
        var sourceProfile = Path.Combine(SampleRoot, "After");
        if (!Directory.Exists(sourceProfile))
        {
            return;
        }

        var profile = WgsProfile.Load(sourceProfile);
        var character = Assert.Single(profile.CharacterSlots);

        Assert.Equal("Tat", character.SlotName);
        Assert.False(string.IsNullOrWhiteSpace(character.Metadata?.CharacterName));
    }

    [Fact]
    [Trait("Category", "LocalFixture")]
    public void ImportReplacesCharacterInTemporaryProfileCopy()
    {
        var sourceProfile = Path.Combine(SampleRoot, "After");
        if (!Directory.Exists(sourceProfile) || !File.Exists(CharacterSavePath))
        {
            return;
        }

        var testRoot = Path.Combine(Path.GetTempPath(), "RSDWSaveConverter.Tests", Guid.NewGuid().ToString("N"));
        var profileCopy = Path.Combine(testRoot, "Profile");
        var backups = Path.Combine(testRoot, "Backups");
        Directory.CreateDirectory(profileCopy);
        CopyDirectory(sourceProfile, profileCopy);

        try
        {
            var beforeSourceHash = Hash(File.ReadAllBytes(Path.Combine(sourceProfile, WgsIndex.FileName)));
            var sourceJson = File.ReadAllBytes(CharacterSavePath);
            var beforeImport = WgsProfile.Load(profileCopy);
            var destination = beforeImport.CharacterSlots.Single();
            var backupBefore = WgsProfile.ReadPayload(
                profileCopy,
                Assert.IsType<WgsIndexEntry>(destination.BackupEntry));
            var importer = new WgsImporter();
            var result = importer.ReplaceCharacter(
                profileCopy,
                destination.ActiveEntry.FileName,
                sourceJson,
                backups);

            Assert.Equal("Beb", result.SaveName);
            Assert.True(File.Exists(result.BackupPath));

            var imported = WgsProfile.Load(profileCopy);
            var slot = Assert.Single(imported.CharacterSlots);
            var installedJson = WgsProfile.ReadPayload(profileCopy, slot.ActiveEntry);
            Assert.Equal(sourceJson, installedJson);
            Assert.Equal("Beb", slot.Metadata?.CharacterName);
            Assert.Equal(
                backupBefore,
                WgsProfile.ReadPayload(profileCopy, Assert.IsType<WgsIndexEntry>(slot.BackupEntry)));
            Assert.Equal(beforeSourceHash, Hash(File.ReadAllBytes(Path.Combine(sourceProfile, WgsIndex.FileName))));
        }
        finally
        {
            var resolved = Path.GetFullPath(testRoot);
            var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "RSDWSaveConverter.Tests"));
            if (resolved.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
            {
                Directory.Delete(resolved, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}
