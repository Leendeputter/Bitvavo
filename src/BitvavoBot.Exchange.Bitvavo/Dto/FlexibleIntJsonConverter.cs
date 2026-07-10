using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitvavoBot.Exchange.Bitvavo.Dto;

/// <summary>
/// Some Bitvavo API fields that are conceptually integers (e.g. pricePrecision) are not
/// guaranteed to arrive as a plain JSON integer — they can show up as a decimal-formatted number
/// (e.g. 5.0) or as a numeric string, either of which makes the default System.Text.Json int
/// conversion throw. This reads any of those forms and truncates to an int.
/// </summary>
public sealed class FlexibleIntJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var intValue)) return intValue;
            return (int)reader.GetDouble();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt)) return parsedInt;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble)) return (int)parsedDouble;
        }

        throw new JsonException($"Cannot convert JSON token {reader.TokenType} to an int.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
