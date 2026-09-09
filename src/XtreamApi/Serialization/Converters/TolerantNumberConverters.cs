using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Reads a number whatever shape it takes on the wire.
/// <para>
/// Xtream panels alternate without logic between <c>42</c>, <c>"42"</c>,
/// <c>""</c> and <c>null</c> for one and the same field, from one action to the
/// next and from one provider to the next. Any strict read eventually throws in
/// production.
/// </para>
/// </summary>
internal static class TolerantNumber
{
    internal static long? ReadInt64(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var number))
                {
                    return number;
                }

                return reader.TryGetDouble(out var asDouble) ? (long)asDouble : null;

            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble)
                    ? (long)parsedDouble
                    : null;

            case JsonTokenType.True:
                return 1;

            case JsonTokenType.False:
            case JsonTokenType.Null:
                return null;

            default:
                // A panel may return an object or an array where a number is
                // expected. The node is consumed so the reader stays in sync, and
                // the value is treated as absent.
                reader.Skip();
                return null;
        }
    }

    internal static double? ReadDouble(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDouble();

            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;

            case JsonTokenType.True:
                return 1d;

            case JsonTokenType.False:
            case JsonTokenType.Null:
                return null;

            default:
                reader.Skip();
                return null;
        }
    }

    internal static bool? ReadBoolean(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;

            case JsonTokenType.False:
                return false;

            case JsonTokenType.Number:
                return reader.TryGetInt64(out var number) ? number != 0 : null;

            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return text.Trim().ToLowerInvariant() switch
                {
                    "1" or "true" or "yes" or "on" => true,
                    "0" or "false" or "no" or "off" => false,
                    _ => null,
                };

            case JsonTokenType.Null:
                return null;

            default:
                reader.Skip();
                return null;
        }
    }
}

internal sealed class TolerantInt32Converter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => (int?)TolerantNumber.ReadInt64(ref reader) ?? 0;

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

internal sealed class TolerantNullableInt32Converter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => (int?)TolerantNumber.ReadInt64(ref reader);

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

internal sealed class TolerantInt64Converter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TolerantNumber.ReadInt64(ref reader) ?? 0L;

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

internal sealed class TolerantNullableInt64Converter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TolerantNumber.ReadInt64(ref reader);

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

internal sealed class TolerantDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TolerantNumber.ReadDouble(ref reader) ?? 0d;

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

internal sealed class TolerantNullableDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TolerantNumber.ReadDouble(ref reader);

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

internal sealed class TolerantBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TolerantNumber.ReadBoolean(ref reader) ?? false;

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}

internal sealed class TolerantNullableBooleanConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TolerantNumber.ReadBoolean(ref reader);

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteBooleanValue(value.Value);
        }
    }
}
