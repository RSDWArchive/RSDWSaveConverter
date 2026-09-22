namespace RSDWSaveConverter.Core.Wgs;

public sealed record WgsWorldSlot(
    string SlotName,
    string DisplayName,
    WgsIndexEntry ActiveEntry,
    WgsIndexEntry? BackupEntry,
    DragonwildsSaveMetadata? Metadata)
{
    public override string ToString() => DisplayName;
}

public sealed record WgsCharacterSlot(
    string SlotName,
    string DisplayName,
    WgsIndexEntry ActiveEntry,
    WgsIndexEntry? BackupEntry,
    DragonwildsCharacterMetadata? Metadata)
{
    public override string ToString() => DisplayName;
}

public sealed class WgsProfile
{
    public const string PackageFamilyName = "JagexLimited.Dominion_srxstwq7wczqa";

    public required string Path { get; init; }
    public required WgsIndex Index { get; init; }
    public required IReadOnlyList<WgsWorldSlot> WorldSlots { get; init; }
    public required IReadOnlyList<WgsCharacterSlot> CharacterSlots { get; init; }

    public static WgsProfile Load(string path)
    {
        var indexPath = System.IO.Path.Combine(path, WgsIndex.FileName);
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("The selected folder does not contain containers.index.", indexPath);
        }

        var index = WgsIndex.Load(indexPath);
        var activeWorldEntries = index.Entries
            .Where(entry => entry.State != WgsEntryState.Deleted)
            .Where(entry => entry.FileName.EndsWith("Qxav", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !entry.FileName.EndsWith("QxavQbak", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var worldSlots = new List<WgsWorldSlot>();
        foreach (var active in activeWorldEntries)
        {
            var slotName = active.FileName[..^4];
            var backup = index.Entries.FirstOrDefault(entry =>
                entry.State != WgsEntryState.Deleted
                && entry.FileName.Equals(active.FileName + "Qbak", StringComparison.OrdinalIgnoreCase));

            DragonwildsSaveMetadata? metadata = null;
            try
            {
                var payload = ReadPayload(path, active);
                var raw = DragonwildsSaveCodec.IsWrappedSave(payload)
                    ? DragonwildsSaveCodec.Unwrap(payload)
                    : payload;
                metadata = DragonwildsSaveCodec.ReadMetadata(raw, slotName);
            }
            catch (Exception)
            {
                // The slot remains selectable even when its preview cannot be decoded.
            }

            worldSlots.Add(new WgsWorldSlot(
                slotName,
                metadata?.WorldName ?? slotName,
                active,
                backup,
                metadata));
        }

        var activeCharacterEntries = index.Entries
            .Where(entry => entry.State != WgsEntryState.Deleted)
            .Where(entry => entry.FileName.EndsWith("Qjson", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var characterSlots = new List<WgsCharacterSlot>();
        foreach (var active in activeCharacterEntries)
        {
            var slotName = active.FileName[..^5];
            var backup = index.Entries.FirstOrDefault(entry =>
                entry.State != WgsEntryState.Deleted
                && entry.FileName.Equals(active.FileName + "Qbak", StringComparison.OrdinalIgnoreCase));

            DragonwildsCharacterMetadata? metadata = null;
            try
            {
                metadata = DragonwildsCharacterCodec.ReadMetadata(ReadPayload(path, active), slotName);
            }
            catch (Exception)
            {
                // The slot remains selectable even when its preview cannot be decoded.
            }

            characterSlots.Add(new WgsCharacterSlot(
                slotName,
                metadata?.CharacterName ?? slotName,
                active,
                backup,
                metadata));
        }

        return new WgsProfile
        {
            Path = path,
            Index = index,
            WorldSlots = worldSlots.OrderBy(slot => slot.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            CharacterSlots = characterSlots.OrderBy(slot => slot.DisplayName, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    public static IReadOnlyList<WgsProfile> Discover()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wgsRoot = System.IO.Path.Combine(
            localAppData,
            "Packages",
            PackageFamilyName,
            "SystemAppData",
            "wgs");

        if (!Directory.Exists(wgsRoot))
        {
            return [];
        }

        var profiles = new List<WgsProfile>();
        foreach (var directory in Directory.EnumerateDirectories(wgsRoot))
        {
            if (!File.Exists(System.IO.Path.Combine(directory, WgsIndex.FileName)))
            {
                continue;
            }

            try
            {
                profiles.Add(Load(directory));
            }
            catch (Exception)
            {
                // Ignore unrelated or unsupported WGS profiles during discovery.
            }
        }

        return profiles
            .OrderByDescending(profile => File.GetLastWriteTimeUtc(System.IO.Path.Combine(profile.Path, WgsIndex.FileName)))
            .ToList();
    }

    public static byte[] ReadPayload(string profilePath, WgsIndexEntry entry)
    {
        var containerDirectory = System.IO.Path.Combine(profilePath, entry.ContainerId.ToWgsName());
        var tablePath = System.IO.Path.Combine(containerDirectory, $"container.{entry.BlobId}");
        var table = WgsBlobTable.Load(tablePath);
        var dataRecord = table.Records.Single(record => record.Name.Equals("Data", StringComparison.OrdinalIgnoreCase));
        var payloadPath = System.IO.Path.Combine(containerDirectory, dataRecord.FileId.ToWgsName());
        return File.ReadAllBytes(payloadPath);
    }
}
