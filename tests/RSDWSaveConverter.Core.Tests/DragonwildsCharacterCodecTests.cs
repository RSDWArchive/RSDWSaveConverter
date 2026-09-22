using System.Text;

namespace RSDWSaveConverter.Core.Tests;

public sealed class DragonwildsCharacterCodecTests
{
    [Fact]
    public void ReadsCharacterNameAndVersion()
    {
        var data = Encoding.UTF8.GetBytes("""
            {
              "Version": 12,
              "meta_data": {
                "char_name": "Test Hero"
              },
              "GameProgress": {}
            }
            """);

        var metadata = DragonwildsCharacterCodec.ReadMetadata(data);

        Assert.Equal("Test Hero", metadata.CharacterName);
        Assert.Equal(12, metadata.Version);
        Assert.Equal(data.Length, metadata.ByteLength);
    }

    [Fact]
    public void RejectsInvalidJson()
    {
        Assert.Throws<InvalidDataException>(() =>
            DragonwildsCharacterCodec.ReadMetadata("not-json"u8));
    }
}
