using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// An episode of a series, from the <c>episodes</c> block of <c>get_series_info</c>.
/// </summary>
public sealed class Episode : CatalogItem
{
    /// <summary>
    /// Stream identifier of the episode. This value, not <c>series_id</c>, is
    /// what appears in the <c>/series/</c> URL.
    /// </summary>
    [JsonPropertyName("id")]
    public int EpisodeId { get; init; }

    /// <summary>Number of the episode within its season.</summary>
    [JsonPropertyName("episode_num")]
    public int EpisodeNumber { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Season number. Missing on some panels, which carry it only in the dictionary key.</summary>
    [JsonPropertyName("season")]
    public int? Season { get; init; }

    [JsonPropertyName("container_extension")]
    public string? ContainerExtension { get; init; }

    [JsonPropertyName("info")]
    public EpisodeDetails? Details { get; init; }

    [JsonIgnore]
    public override StreamKind Kind => StreamKind.Series;

    [JsonIgnore]
    public override int Id => EpisodeId;

    /// <summary>Title to display: <c>title</c> when present, <c>name</c> otherwise.</summary>
    [JsonIgnore]
    public string? DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Name : Title;
}

/// <summary>
/// The <c>info</c> block of an episode.
/// </summary>
public sealed class EpisodeDetails
{
    [JsonPropertyName("tmdb_id")]
    public string? TmdbId { get; init; }

    [JsonPropertyName("releasedate")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? ReleaseDate { get; init; }

    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonPropertyName("duration_secs")]
    public int? DurationSeconds { get; init; }

    /// <summary>Human-readable duration, in <c>hh:mm:ss</c> form.</summary>
    [JsonPropertyName("duration")]
    public string? DurationText { get; init; }

    [JsonPropertyName("movie_image")]
    public string? ImageUrl { get; init; }

    [JsonPropertyName("cover_big")]
    public string? CoverBigUrl { get; init; }

    [JsonPropertyName("bitrate")]
    public int? Bitrate { get; init; }

    [JsonPropertyName("rating")]
    public double? Rating { get; init; }

    [JsonPropertyName("season")]
    public int? Season { get; init; }

    [JsonIgnore]
    public TimeSpan? Duration =>
        DurationSeconds is > 0 ? TimeSpan.FromSeconds(DurationSeconds.Value) : null;
}

/// <summary>
/// A season, from the <c>seasons</c> block of <c>get_series_info</c>.
/// <para>
/// This block is often empty even when the series has episodes: the seasons
/// actually available are the keys of the <see cref="SeriesInfo.Episodes"/>
/// dictionary, not this list.
/// </para>
/// </summary>
public sealed class Season
{
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("season_number")]
    public int Number { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("air_date")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? AirDate { get; init; }

    [JsonPropertyName("episode_count")]
    public int? EpisodeCount { get; init; }

    [JsonPropertyName("vote_average")]
    public double? VoteAverage { get; init; }

    [JsonPropertyName("cover")]
    public string? CoverUrl { get; init; }

    [JsonPropertyName("cover_big")]
    public string? CoverBigUrl { get; init; }
}
