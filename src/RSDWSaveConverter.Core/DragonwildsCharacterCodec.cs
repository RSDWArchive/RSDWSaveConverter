using System.Text.Json;

namespace RSDWSaveConverter.Core;

public sealed record DragonwildsCharacterMetadata(
    string CharacterName,
    int? Version,
    int ByteLength);

public static class DragonwildsCharacterCodec
{
    public static byte[] ReadCharacter(string path)
    {
        var data = File.ReadAllBytes(path);
        _ = ReadMetadata(data, Path.GetFileNameWithoutExtension(path));
        return data;
    }

    public static DragonwildsCharacterMetadata ReadMetadata(
        ReadOnlySpan<byte> data,
        string? fallbackName = null)
    {
        if (data.IsEmpty)
        {
            throw new InvalidDataException("The character file is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(data.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The character file must contain a JSON object.");
            }

            if (!TryGetProperty(root, "meta_data", out var metadata)
                || metadata.ValueKind != JsonValueKind.Object
                || !TryGetProperty(root, "GameProgress", out var gameProgress)
                || gameProgress.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The selected JSON does not have the Dragonwilds character-save structure.");
            }

            var characterName = TryGetProperty(metadata, "char_name", out var name)
                && name.ValueKind == JsonValueKind.String
                    ? name.GetString()
                    : null;

            characterName = string.IsNullOrWhiteSpace(characterName)
                ? fallbackName
                : characterName;
            if (string.IsNullOrWhiteSpace(characterName))
            {
                throw new InvalidDataException("The character file does not contain meta_data.char_name.");
            }

            int? version = null;
            if (TryGetProperty(root, "Version", out var versionElement)
                && versionElement.ValueKind == JsonValueKind.Number
                && versionElement.TryGetInt32(out var parsedVersion))
            {
                version = parsedVersion;
            }

            return new DragonwildsCharacterMetadata(characterName, version, data.Length);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The selected file is not valid Dragonwilds character JSON.", exception);
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
