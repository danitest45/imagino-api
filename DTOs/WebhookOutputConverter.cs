using System.Text.Json;
using System.Text.Json.Serialization;

namespace Imagino.Api.DTOs;

public sealed class WebhookOutputConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var value = JsonDocument.ParseValue(ref reader);
        var root = value.RootElement;
        if (root.ValueKind == JsonValueKind.Null) return null;
        if (root.ValueKind == JsonValueKind.String) return root.GetString();
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.String)
            return root[0].GetString();
        throw new JsonException("Unsupported provider output.");
    }
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
