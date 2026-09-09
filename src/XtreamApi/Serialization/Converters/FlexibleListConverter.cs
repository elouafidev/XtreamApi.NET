using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Reads a list of objects that is not always an array.
/// <para>
/// PHP does not distinguish an array from a dictionary: <c>json_encode</c>
/// returns a JSON array when the keys are 0, 1, 2..., and an object as soon as
/// one entry has been removed. The same field therefore arrives sometimes as
/// <c>[...]</c>, sometimes as <c>{"0":...,"2":...}</c>, sometimes as <c>[]</c>
/// when empty, depending on the state of the provider's database.
/// </para>
/// <para>
/// Always returns a list, empty rather than null.
/// </para>
/// </summary>
public sealed class FlexibleListConverter<T> : JsonConverter<IReadOnlyList<T>>
{
    /// <summary>
    /// Without this setting, System.Text.Json assigns <c>null</c> straight to the
    /// property without going through the converter.
    /// </summary>
    public override bool HandleNull => true;

    public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var items = new List<T>();

        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return items;

            case JsonTokenType.StartArray:
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    ReadItem(ref reader, options, items);
                }

                return items;

            case JsonTokenType.StartObject:
                // Indexed object: the keys carry no useful information, only the
                // values matter.
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    reader.Read();
                    ReadItem(ref reader, options, items);
                }

                return items;

            default:
                reader.Skip();
                return items;
        }
    }

    private static void ReadItem(ref Utf8JsonReader reader, JsonSerializerOptions options, List<T> items)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return;
        }

        var item = JsonSerializer.Deserialize<T>(ref reader, options);

        if (item is not null)
        {
            items.Add(item);
        }
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
