using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using XtreamApi.Models;

namespace XtreamApi.Serialization.Converters;

/// <summary>
/// Reads the <c>episodes</c> field of <c>get_series_info</c>, which is not an
/// array.
/// <para>
/// Xtream returns an object whose keys are the season numbers:
/// <c>{"1": [episode, ...], "2": [episode, ...]}</c>. Naively deserialising it
/// as an array fails.
/// </para>
/// <para>
/// Two further variants exist in the wild: an empty array <c>[]</c> when the
/// series has no episodes (an artefact of PHP's <c>json_encode</c> on an empty
/// array), and an array of arrays indexed by position. Both are accepted.
/// </para>
/// </summary>
public sealed class SeasonEpisodesConverter : JsonConverter<IReadOnlyDictionary<int, IReadOnlyList<Episode>>>
{
    /// <summary>
    /// Without this setting, System.Text.Json assigns <c>null</c> straight to the
    /// property without going through the converter: a missing field would become
    /// null again instead of yielding an empty collection.
    /// </summary>
    public override bool HandleNull => true;

    public override IReadOnlyDictionary<int, IReadOnlyList<Episode>> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var seasons = new Dictionary<int, IReadOnlyList<Episode>>();

        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return seasons;

            case JsonTokenType.StartObject:
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    var key = reader.GetString();
                    reader.Read();

                    var episodes = ReadEpisodeArray(ref reader, options);

                    // A non-numeric key is not a season: it is skipped rather
                    // than failing the whole series.
                    if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seasonNumber))
                    {
                        seasons[seasonNumber] = episodes;
                    }
                }

                return seasons;

            case JsonTokenType.StartArray:
                var position = 0;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    position++;
                    var episodes = ReadEpisodeArray(ref reader, options);
                    if (episodes.Count > 0)
                    {
                        // Episodes carry their own season number: trust it
                        // before falling back on the position.
                        var seasonNumber = episodes[0].Season ?? position;
                        seasons[seasonNumber] = episodes;
                    }
                }

                return seasons;

            default:
                reader.Skip();
                return seasons;
        }
    }

    private static IReadOnlyList<Episode> ReadEpisodeArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            // A season holding a single episode is sometimes returned as one
            // object rather than a one-element array.
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var single = JsonSerializer.Deserialize<Episode>(ref reader, options);
                return single is null ? [] : [single];
            }

            reader.Skip();
            return [];
        }

        var episodes = new List<Episode>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var episode = JsonSerializer.Deserialize<Episode>(ref reader, options);
                if (episode is not null)
                {
                    episodes.Add(episode);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return episodes;
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<int, IReadOnlyList<Episode>> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (season, episodes) in value)
        {
            writer.WritePropertyName(season.ToString(CultureInfo.InvariantCulture));
            JsonSerializer.Serialize(writer, episodes, options);
        }

        writer.WriteEndObject();
    }
}
