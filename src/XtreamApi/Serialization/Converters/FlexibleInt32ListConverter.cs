using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Reads a list of integers, tolerant of absence and of null.
/// <para>
/// <c>category_ids</c> exists only on recent panels; elsewhere it is missing or
/// <c>null</c>. Returning an empty list rather than <c>null</c> avoids spreading
/// a null check through all the calling code.
/// </para>
/// </summary>
public sealed class FlexibleInt32ListConverter : JsonConverter<IReadOnlyList<int>>
{
    /// <summary>
    /// Without this setting, System.Text.Json assigns <c>null</c> straight to the
    /// property without going through the converter: a missing field would become
    /// null again instead of yielding an empty collection.
    /// </summary>
    public override bool HandleNull => true;

    public override IReadOnlyList<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return [];

            case JsonTokenType.Number:
            case JsonTokenType.String:
                var single = TolerantNumber.ReadInt64(ref reader);
                return single is null ? [] : [(int)single.Value];

            case JsonTokenType.StartArray:
                var items = new List<int>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    var value = TolerantNumber.ReadInt64(ref reader);
                    if (value is not null)
                    {
                        items.Add((int)value.Value);
                    }
                }

                return items;

            default:
                reader.Skip();
                return [];
        }
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<int> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }
}
