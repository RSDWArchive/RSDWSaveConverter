using System.Buffers.Binary;

namespace RSDWSaveConverter.Core.Tests;

public sealed class DragonwildsSaveCodecTests
{
    [Fact]
    public void WrapAndUnwrapRoundTrip()
    {
        var raw = "SAVE"u8.ToArray().Concat(Enumerable.Range(0, 4096).Select(value => (byte)value)).ToArray();

        var wrapped = DragonwildsSaveCodec.Wrap(raw);
        var result = DragonwildsSaveCodec.Unwrap(wrapped);

        Assert.Equal(12, BinaryPrimitives.ReadInt32LittleEndian(wrapped));
        Assert.Equal(65_536, BinaryPrimitives.ReadInt32LittleEndian(wrapped.AsSpan(4)));
        Assert.Equal(raw.Length, BinaryPrimitives.ReadInt32LittleEndian(wrapped.AsSpan(8)));
        Assert.Equal(raw, result);
    }

    [Fact]
    public void RejectsFilesWithoutSaveSignature()
    {
        Assert.Throws<InvalidDataException>(() => DragonwildsSaveCodec.Wrap("NOPE"u8));
    }
}
