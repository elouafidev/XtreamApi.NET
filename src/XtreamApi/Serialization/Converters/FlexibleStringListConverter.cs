using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Reads a list of strings that is not always one.
/// <para>
/// <c>backdrop_path</c> is an array on most panels but a plain string on others;
/// <c>allowed_output_formats</c> may be <c>null</c> or missing. The same field
/// sometimes changes shape between two items of a single response.
/// </para>
/// <para>
/// Always returns a list, empty rather than <c>null</c>, so the caller never has
/// to check for null before iterating.
/// </para>
/// </summary>
public sealed class FlexibleStringListConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <summary>
    /// Without this setting, System.Text.Json assigns <c>null</c> straight to the
    /// property without going through the converter: a missing field would become
    /// null again instead of yielding an empty collection.
    /// </summary>
    public override bool HandleNull => true;

    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return [];

            case JsonTokenType.String:
                var single = reader.GetString();
                return string.IsNullOrWhiteSpace(single) ? [] : [single];

            case JsonTokenType.Number:
                return [reader.GetDouble().ToString(CultureInfo.InvariantCulture)];

            case JsonTokenType.StartArray:
                var items = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.String:
                            var value = reader.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                items.Add(value);
                            }

                            break;

                        case JsonTokenType.Number:
                            items.Add(reader.GetDouble().ToString(CultureInfo.InvariantCulture));
                            break;

                        case JsonTokenType.Null:
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }

                return items;

            default:
                reader.Skip();
                return [];
        }
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
