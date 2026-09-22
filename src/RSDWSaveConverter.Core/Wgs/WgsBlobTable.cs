using System.Text;

namespace RSDWSaveConverter.Core.Wgs;

public sealed record WgsBlobRecord(string Name, Guid AtomId, Guid FileId);

public sealed class WgsBlobTable
{
    public const uint SupportedVersion = 4;
    public List<WgsBlobRecord> Records { get; } = [];

    public static WgsBlobTable Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        var table = Read(reader);
        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("The WGS blob table contains unsupported trailing data.");
        }

        return table;
    }

    public static WgsBlobTable Read(BinaryReader reader)
    {
        var version = reader.ReadUInt32();
        if (version != SupportedVersion)
        {
            throw new NotSupportedException($"WGS blob table version {version} is not supported.");
        }

        var count = reader.ReadUInt32();
        if (count > 1_000)
        {
            throw new InvalidDataException($"Invalid WGS blob count: {count}.");
        }

        var table = new WgsBlobTable();
        for (var position = 0; position < count; position++)
        {
            var nameBytes = reader.ReadBytes(128);
            if (nameBytes.Length != 128)
            {
                throw new EndOfStreamException("Unexpected end of a WGS blob record.");
            }

            var characterLength = 0;
            while (characterLength < 64 && nameBytes[characterLength * 2] != 0)
            {
                characterLength++;
            }

            var name = Encoding.Unicode.GetString(nameBytes, 0, characterLength * 2);
            var atomId = new Guid(reader.ReadBytes(16));
            var fileId = new Guid(reader.ReadBytes(16));
            table.Records.Add(new WgsBlobRecord(name, atomId, fileId));
        }

        return table;
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(SupportedVersion);
        writer.Write((uint)Records.Count);

        foreach (var record in Records)
        {
            var nameBytes = Encoding.Unicode.GetBytes(record.Name);
            if (nameBytes.Length > 128)
            {
                throw new InvalidDataException("WGS blob names are limited to 64 characters.");
            }

            var fixedName = new byte[128];
            nameBytes.CopyTo(fixedName, 0);
            writer.Write(fixedName);
            writer.Write(record.AtomId.ToByteArray());
            writer.Write(record.FileId.ToByteArray());
        }

        writer.Flush();
        return stream.ToArray();
    }
}
