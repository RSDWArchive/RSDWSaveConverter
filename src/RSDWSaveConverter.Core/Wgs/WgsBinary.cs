using System.Text;

namespace RSDWSaveConverter.Core.Wgs;

internal static class WgsBinary
{
    public static string ReadUtf16String(this BinaryReader reader, int maximumLength = int.MaxValue)
    {
        var length = reader.ReadInt32();
        if (length < 0 || length > maximumLength)
        {
            throw new InvalidDataException($"Invalid WGS string length {length} (maximum {maximumLength}).");
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var bytes = reader.ReadBytes(checked(length * 2));
        if (bytes.Length != length * 2)
        {
            throw new EndOfStreamException("Unexpected end of WGS metadata while reading a string.");
        }

        return Encoding.Unicode.GetString(bytes);
    }

    public static void WriteUtf16String(this BinaryWriter writer, string value, int maximumLength = int.MaxValue)
    {
        if (value.Length > maximumLength)
        {
            throw new InvalidDataException($"WGS string exceeds its {maximumLength}-character limit.");
        }

        writer.Write(value.Length);
        if (value.Length > 0)
        {
            writer.Write(Encoding.Unicode.GetBytes(value));
        }
    }

    public static string ToWgsName(this Guid value) => value.ToString("N").ToUpperInvariant();
}
