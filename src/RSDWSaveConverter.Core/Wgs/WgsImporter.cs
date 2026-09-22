using System.Diagnostics;
using System.IO.Compression;

namespace RSDWSaveConverter.Core.Wgs;

public sealed record WgsImportResult(
    string SaveName,
    string DestinationSlot,
    string BackupPath,
    int PayloadSize);

public sealed class WgsImporter
{
    public static bool IsDragonwildsRunning() => Process.GetProcesses().Any(process =>
    {
        try
        {
            return process.ProcessName.Contains("RSDragonwilds", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Equals("Dominion", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    });

    public WgsImportResult ReplaceWorld(
        string profilePath,
        string destinationFileName,
        ReadOnlySpan<byte> rawSave,
        string? backupRoot = null)
    {
        if (!destinationFileName.EndsWith("Qxav", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected destination is not a Game Pass world slot.");
        }

        var metadata = DragonwildsSaveCodec.ReadMetadata(rawSave);
        var wrapped = DragonwildsSaveCodec.Wrap(rawSave);
        return ReplaceSave(
            profilePath,
            destinationFileName,
            rawSave.ToArray(),
            wrapped,
            static data => DragonwildsSaveCodec.Unwrap(data),
            metadata.WorldName,
            "world",
            backupRoot);
    }

    public WgsImportResult ReplaceCharacter(
        string profilePath,
        string destinationFileName,
        ReadOnlySpan<byte> characterJson,
        string? backupRoot = null)
    {
        if (!destinationFileName.EndsWith("Qjson", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected destination is not a Game Pass character slot.");
        }

        var metadata = DragonwildsCharacterCodec.ReadMetadata(characterJson);
        var payload = characterJson.ToArray();
        return ReplaceSave(
            profilePath,
            destinationFileName,
            payload,
            payload,
            static data => data,
            metadata.CharacterName,
            "character",
            backupRoot);
    }

    private static WgsImportResult ReplaceSave(
        string profilePath,
        string destinationFileName,
        byte[] sourceData,
        byte[] installedPayload,
        Func<byte[], byte[]> readInstalledSource,
        string saveName,
        string saveType,
        string? backupRoot)
    {
        if (IsDragonwildsRunning())
        {
            throw new InvalidOperationException($"Close RuneScape: Dragonwilds before importing a {saveType}.");
        }

        var backupPath = BackupProfile(profilePath, backupRoot);
        var indexPath = System.IO.Path.Combine(profilePath, WgsIndex.FileName);
        var originalIndexBytes = File.ReadAllBytes(indexPath);
        var index = WgsIndex.Load(indexPath);
        var entry = index.Entries.SingleOrDefault(candidate =>
            candidate.FileName.Equals(destinationFileName, StringComparison.OrdinalIgnoreCase)
            && !candidate.FileName.EndsWith("Qbak", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Game Pass {saveType} slot '{destinationFileName}' was not found.");

        var containerDirectory = System.IO.Path.Combine(profilePath, entry.ContainerId.ToWgsName());
        var currentTablePath = System.IO.Path.Combine(containerDirectory, $"container.{entry.BlobId}");
        var currentTable = WgsBlobTable.Load(currentTablePath);
        var currentData = currentTable.Records.Single(record =>
            record.Name.Equals("Data", StringComparison.OrdinalIgnoreCase));

        var nextBlobId = FindAvailableBlobId(containerDirectory, entry.BlobId);
        var nextFileId = Guid.NewGuid();
        var nextTable = new WgsBlobTable();
        nextTable.Records.Add(new WgsBlobRecord("Data", currentData.AtomId, nextFileId));

        var payloadPath = System.IO.Path.Combine(containerDirectory, nextFileId.ToWgsName());
        var tablePath = System.IO.Path.Combine(containerDirectory, $"container.{nextBlobId}");
        var now = DateTime.UtcNow.ToFileTimeUtc();

        try
        {
            WriteNewFile(payloadPath, installedPayload);
            WriteNewFile(tablePath, nextTable.ToBytes());

            entry.BlobId = nextBlobId;
            entry.FileSize = installedPayload.Length;
            entry.LastModifiedFileTimeUtc = now;
            entry.State = WgsEntryState.Modified;
            index.LastModifiedFileTime = now;
            index.Flags = WgsSyncFlags.FullyDownloaded;

            AtomicReplace(indexPath, index.ToBytes());

            var verified = WgsProfile.Load(profilePath);
            var verifiedEntry = verified.Index.Entries.Single(candidate =>
                candidate.FileName.Equals(destinationFileName, StringComparison.OrdinalIgnoreCase));
            var verifiedSource = readInstalledSource(WgsProfile.ReadPayload(profilePath, verifiedEntry));
            if (!verifiedSource.AsSpan().SequenceEqual(sourceData))
            {
                throw new InvalidDataException($"Post-import verification did not reproduce the selected {saveType} save.");
            }
        }
        catch
        {
            AtomicReplace(indexPath, originalIndexBytes);
            TryDelete(payloadPath);
            TryDelete(tablePath);
            throw;
        }

        return new WgsImportResult(saveName, destinationFileName, backupPath, installedPayload.Length);
    }

    public static string BackupProfile(string profilePath, string? backupRoot = null)
    {
        backupRoot ??= System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RSDWSaveConverter",
            "Backups");
        Directory.CreateDirectory(backupRoot);

        var profileName = new DirectoryInfo(profilePath).Name;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var output = System.IO.Path.Combine(backupRoot, $"{profileName}-{timestamp}.zip");
        var suffix = 2;
        while (File.Exists(output))
        {
            output = System.IO.Path.Combine(backupRoot, $"{profileName}-{timestamp}-{suffix++}.zip");
        }

        ZipFile.CreateFromDirectory(profilePath, output, CompressionLevel.Fastest, includeBaseDirectory: true);
        return output;
    }

    private static byte FindAvailableBlobId(string containerDirectory, byte current)
    {
        for (var offset = 1; offset < byte.MaxValue; offset++)
        {
            var candidate = (byte)(((current - 1 + offset) % byte.MaxValue) + 1);
            if (!File.Exists(System.IO.Path.Combine(containerDirectory, $"container.{candidate}")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No free WGS blob-table identifier is available.");
    }

    private static void WriteNewFile(string path, ReadOnlySpan<byte> data)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(data);
        stream.Flush(flushToDisk: true);
    }

    private static void AtomicReplace(string destination, ReadOnlySpan<byte> data)
    {
        var temporary = destination + ".rsdw-tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }

            File.Replace(temporary, destination, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; the complete profile backup remains available.
        }
    }
}
