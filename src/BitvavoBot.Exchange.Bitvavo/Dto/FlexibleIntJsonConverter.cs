using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitvavoBot.Exchange.Bitvavo.Dto;

/// <summary>
/// Some Bitvavo API fields that are conceptually integers (e.g. pricePrecision) are not
/// guaranteed to arrive as a plain JSON integer — they can show up as a decimal-formatted number
/// (e.g. 5.0), a numeric string, or null for some markets. Any of those make the default
/// System.Text.Json int conversion throw. This reads any of those forms and truncates to an int,
/// falling back to a generous 8-decimal default on null (used for price rounding — falling back to
/// 0 would wrongly truncate every price to a whole number instead of just not rounding tightly enough).
/// </summary>
public sealed class FlexibleIntJsonConverter : JsonConverter<int>
{
    private const int NullFallback = 8;

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return NullFallback;

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var intValue)) return intValue;
            return (int)reader.GetDouble();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrEmpty(text)) return NullFallback;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt)) return parsedInt;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble)) return (int)parsedDouble;
        }

        throw new JsonException($"Cannot convert JSON token {reader.TokenType} to an int.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
