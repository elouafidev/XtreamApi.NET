using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// Response of <c>get_series_info</c>: the detailed record of a series and its
/// episodes.
/// </summary>
public sealed class SeriesInfo
{
    [JsonPropertyName("info")]
    public SeriesDetails? Details { get; init; }

    /// <summary>
    /// Seasons declared by the panel. Frequently empty: rely on the keys of
    /// <see cref="Episodes"/> to know what is actually available.
    /// </summary>
    [JsonPropertyName("seasons")]
    [JsonConverter(typeof(FlexibleListConverter<Season>))]
    public IReadOnlyList<Season> Seasons { get; init; } = [];

    /// <summary>
    /// Episodes indexed by season number. On the wire this is not an array but
    /// an object whose keys are the season numbers.
    /// </summary>
    [JsonPropertyName("episodes")]
    [JsonConverter(typeof(SeasonEpisodesConverter))]
    public IReadOnlyDictionary<int, IReadOnlyList<Episode>> Episodes { get; init; }
        = new Dictionary<int, IReadOnlyList<Episode>>();

    /// <summary>Season numbers actually present, sorted.</summary>
    [JsonIgnore]
    public IReadOnlyList<int> SeasonNumbers => [.. Episodes.Keys.Order()];

    /// <summary>
    /// Every episode flattened, ordered by season then by episode number.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Episode> AllEpisodes =>
    [
        .. Episodes
            .OrderBy(season => season.Key)
            .SelectMany(season => season.Value.OrderBy(episode => episode.EpisodeNumber)),
    ];

    /// <summary>Episodes of a given season, empty when the season does not exist.</summary>
    public IReadOnlyList<Episode> GetSeason(int seasonNumber) =>
        Episodes.TryGetValue(seasonNumber, out var episodes) ? episodes : [];
}

/// <summary>
/// The <c>info</c> block of <c>get_series_info</c>: the editorial metadata of
/// the series.
/// </summary>
public sealed class SeriesDetails
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("cover")]
    public string? CoverUrl { get; init; }

    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonPropertyName("cast")]
    public string? Cast { get; init; }

    [JsonPropertyName("director")]
    public string? Director { get; init; }

    [JsonPropertyName("genre")]
    public string? Genre { get; init; }

    /// <summary>See <see cref="ReleaseDate"/>: panels use one name or the other.</summary>
    [JsonPropertyName("releaseDate")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? ReleaseDateCamelCase { get; init; }

    /// <summary>See <see cref="ReleaseDate"/>.</summary>
    [JsonPropertyName("release_date")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? ReleaseDateSnakeCase { get; init; }

    [JsonPropertyName("last_modified")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTimeOffset? LastModified { get; init; }

    [JsonPropertyName("rating")]
    public double? Rating { get; init; }

    [JsonPropertyName("rating_5based")]
    public double? Rating5Based { get; init; }

    [JsonPropertyName("backdrop_path")]
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public IReadOnlyList<string> BackdropPaths { get; init; } = [];

    [JsonPropertyName("youtube_trailer")]
    public string? YoutubeTrailer { get; init; }

    /// <summary>Typical episode running time, in minutes.</summary>
    [JsonPropertyName("episode_run_time")]
    public int? EpisodeRunTime { get; init; }

    [JsonPropertyName("category_id")]
    public int? CategoryId { get; init; }

    [JsonIgnore]
    public DateTimeOffset? ReleaseDate => ReleaseDateCamelCase ?? ReleaseDateSnakeCase;

    [JsonIgnore]
    public string? DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Name : Title;
}
