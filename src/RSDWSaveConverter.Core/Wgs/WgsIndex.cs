namespace RSDWSaveConverter.Core.Wgs;

[Flags]
public enum WgsSyncFlags : uint
{
    None = 0,
    FullyUploaded = 1,
    FullyDownloaded = 2,
    HasUnresolvedConflicts = 16
}

public enum WgsEntryState : uint
{
    None = 0,
    Synched = 1,
    Unknown = 2,
    Deleted = 3,
    Created = 4,
    Modified = 5
}

public sealed class WgsIndexEntry
{
    public required string FileName { get; set; }
    public required string EntryName { get; set; }
    public required string ETag { get; set; }
    public byte BlobId { get; set; }
    public WgsEntryState State { get; set; }
    public Guid ContainerId { get; set; }
    public long LastModifiedFileTimeUtc { get; set; }
    public uint Type { get; set; }
    public uint Unknown { get; set; }
    public long FileSize { get; set; }

    public DateTime LastModifiedUtc => DateTime.FromFileTimeUtc(LastModifiedFileTimeUtc);
}

public sealed class WgsIndex
{
    public const string FileName = "containers.index";

    public uint Version { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string Aumid { get; set; } = string.Empty;
    public long LastModifiedFileTime { get; set; }
    public WgsSyncFlags Flags { get; set; }
    public string RootContainerIdText { get; set; } = string.Empty;
    public byte[] Reserved { get; set; } = [];
    public List<WgsIndexEntry> Entries { get; } = [];

    public static WgsIndex Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        var index = Read(reader);
        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("The WGS index contains unsupported trailing data.");
        }

        return index;
    }

    public static WgsIndex Read(BinaryReader reader)
    {
        var version = reader.ReadUInt32();
        var entryCount = reader.ReadInt32();
        if (version != 14)
        {
            throw new NotSupportedException($"WGS index version {version} is not supported. Version 14 is required.");
        }

        if (entryCount is < 0 or > 10_000)
        {
            throw new InvalidDataException($"Invalid WGS entry count: {entryCount}.");
        }

        var index = new WgsIndex
        {
            Version = version,
            Name = reader.ReadUtf16String(),
            Aumid = reader.ReadUtf16String(130),
            LastModifiedFileTime = reader.ReadInt64(),
            Flags = (WgsSyncFlags)reader.ReadUInt32(),
            RootContainerIdText = reader.ReadUtf16String(),
            Reserved = reader.ReadBytes(8)
        };

        if (index.Reserved.Length != 8)
        {
            throw new EndOfStreamException("Unexpected end of the WGS index header.");
        }

        for (var position = 0; position < entryCount; position++)
        {
            index.Entries.Add(new WgsIndexEntry
            {
                FileName = reader.ReadUtf16String(255),
                EntryName = reader.ReadUtf16String(127),
                ETag = reader.ReadUtf16String(256),
                BlobId = reader.ReadByte(),
                State = (WgsEntryState)reader.ReadUInt32(),
                ContainerId = new Guid(reader.ReadBytes(16)),
                LastModifiedFileTimeUtc = reader.ReadInt64(),
                Type = reader.ReadUInt32(),
                Unknown = reader.ReadUInt32(),
                FileSize = reader.ReadInt64()
            });
        }

        return index;
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Version);
        writer.Write(Entries.Count);
        writer.WriteUtf16String(Name);
        writer.WriteUtf16String(Aumid, 130);
        writer.Write(LastModifiedFileTime);
        writer.Write((uint)Flags);
        writer.WriteUtf16String(RootContainerIdText);

        if (Reserved.Length != 8)
        {
            throw new InvalidDataException("A WGS v14 index must retain its eight reserved bytes.");
        }

        writer.Write(Reserved);
        foreach (var entry in Entries)
        {
            writer.WriteUtf16String(entry.FileName, 255);
            writer.WriteUtf16String(entry.EntryName, 127);
            writer.WriteUtf16String(entry.ETag, 256);
            writer.Write(entry.BlobId);
            writer.Write((uint)entry.State);
            writer.Write(entry.ContainerId.ToByteArray());
            writer.Write(entry.LastModifiedFileTimeUtc);
            writer.Write(entry.Type);
            writer.Write(entry.Unknown);
            writer.Write(entry.FileSize);
        }
    }
}
