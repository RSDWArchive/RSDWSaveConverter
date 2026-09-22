using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace RSDWSaveConverter.Core;

public sealed record DragonwildsSaveMetadata(
    string WorldName,
    string? SavedAtUtc,
    long UncompressedSize);

public static partial class DragonwildsSaveCodec
{
    public const int WrapperHeaderSize = 12;
    public const int CompressionBlockSize = 65_536;

    private static ReadOnlySpan<byte> SaveMagic => "SAVE"u8;

    public static bool IsRawSave(ReadOnlySpan<byte> data) => data.StartsWith(SaveMagic);

    public static bool IsWrappedSave(ReadOnlySpan<byte> data)
    {
        if (data.Length < WrapperHeaderSize + 2)
        {
            return false;
        }

        return BinaryPrimitives.ReadInt32LittleEndian(data) == WrapperHeaderSize
            && BinaryPrimitives.ReadInt32LittleEndian(data[4..]) == CompressionBlockSize
            && data[12] == 0x78;
    }

    public static byte[] ReadRawSave(string path)
    {
        var data = File.ReadAllBytes(path);
        return IsWrappedSave(data) ? Unwrap(data) : ValidateRawSave(data);
    }

    public static byte[] Wrap(ReadOnlySpan<byte> rawSave)
    {
        ValidateRawSave(rawSave);

        using var output = new MemoryStream();
        Span<byte> header = stackalloc byte[WrapperHeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(header, WrapperHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], CompressionBlockSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], rawSave.Length);
        output.Write(header);

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(rawSave);
        }

        return output.ToArray();
    }

    public static byte[] Unwrap(ReadOnlySpan<byte> wrappedSave)
    {
        if (!IsWrappedSave(wrappedSave))
        {
            throw new InvalidDataException("The file is not a supported Dragonwilds Game Pass payload.");
        }

        var expectedLength = BinaryPrimitives.ReadInt32LittleEndian(wrappedSave[8..]);
        if (expectedLength <= 0)
        {
            throw new InvalidDataException("The Game Pass payload has an invalid uncompressed length.");
        }

        using var input = new MemoryStream(wrappedSave[WrapperHeaderSize..].ToArray(), writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expectedLength);
        zlib.CopyTo(output);
        var rawSave = output.ToArray();

        if (rawSave.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"The Game Pass payload declared {expectedLength:N0} bytes but produced {rawSave.Length:N0} bytes.");
        }

        return ValidateRawSave(rawSave);
    }

    public static DragonwildsSaveMetadata ReadMetadata(ReadOnlySpan<byte> rawSave, string? fallbackName = null)
    {
        ValidateRawSave(rawSave);

        var worldName = string.IsNullOrWhiteSpace(fallbackName) ? "Imported World" : fallbackName.Trim();
        string? savedAt = null;

        if (rawSave.Length >= 16 && rawSave.Slice(8, 4).SequenceEqual("INFO"u8))
        {
            var infoLength = BinaryPrimitives.ReadInt32LittleEndian(rawSave[12..]);
            if (infoLength > 0 && infoLength <= rawSave.Length - 16)
            {
                var infoText = Encoding.Latin1.GetString(rawSave.Slice(16, infoLength));
                savedAt = TimestampRegex().Match(infoText) is { Success: true } timestamp
                    ? timestamp.Value
                    : null;

                var revisionOffset = infoText.IndexOf("Meta_SaveFileRevision", StringComparison.Ordinal);
                if (revisionOffset >= 0)
                {
                    var values = PrintableTextRegex().Matches(infoText[(revisionOffset + 21)..]);
                    if (values.Count > 0)
                    {
                        var candidate = values[0].Value.Trim();
                        if (candidate.Length is > 0 and <= 100)
                        {
                            worldName = candidate;
                        }
                    }
                }
            }
        }

        return new DragonwildsSaveMetadata(worldName, savedAt, rawSave.Length);
    }

    public static string MakeSafeSlotName(string worldName)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(worldName
            .Where(character => !invalid.Contains(character) && !char.IsControl(character))
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(cleaned) ? "Imported World" : cleaned[..Math.Min(cleaned.Length, 96)];
    }

    private static byte[] ValidateRawSave(byte[] rawSave)
    {
        ValidateRawSave(rawSave.AsSpan());
        return rawSave;
    }

    private static void ValidateRawSave(ReadOnlySpan<byte> rawSave)
    {
        if (!IsRawSave(rawSave))
        {
            throw new InvalidDataException("The selected file is not a Dragonwilds world save (missing SAVE signature).");
        }

        if (rawSave.Length > int.MaxValue)
        {
            throw new InvalidDataException("The selected save is too large to convert.");
        }
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"[\x20-\x7E]{3,100}", RegexOptions.CultureInvariant)]
    private static partial Regex PrintableTextRegex();
}
