using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Reads a string even when the panel sends something else.
/// <para>
/// Tolerating numbers was not enough: the reverse happens just as often. Fields
/// textual by nature -- <c>tmdb_id</c>, <c>year</c>, <c>custom_sid</c> -- come
/// out as numbers at some providers, depending on whether the column is empty in
/// their database. System.Text.Json then refuses the conversion, and the whole
/// response fails over a field nobody uses.
/// </para>
/// <para>
/// The value is restored exactly as it appears on the wire: the raw bytes of the
/// number, without going through a numeric type, so neither leading zeros nor
/// the precision of an identifier too large for a <c>long</c> is lost.
/// </para>
/// </summary>
public sealed class TolerantStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
                return reader.HasValueSequence
                    ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
                    : Encoding.UTF8.GetString(reader.ValueSpan);

            case JsonTokenType.True:
                return "true";

            case JsonTokenType.False:
                return "false";

            case JsonTokenType.Null:
                return null;

            default:
                // An object or an array where text is expected: the node is
                // consumed so the reader stays in sync.
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// A globally registered converter also serves dictionary keys. Without
    /// these two overrides, the default implementation throws as soon as a
    /// dictionary has string keys.
    /// </summary>
    public override string ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => reader.GetString() ?? string.Empty;

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        string? value,
        JsonSerializerOptions options) => writer.WritePropertyName(value ?? string.Empty);
}
