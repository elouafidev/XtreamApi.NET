using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// A series from the catalogue, as returned by <c>get_series</c>.
/// <para>
/// This item carries no playable stream identifier: to play an episode you must
/// call <c>get_series_info</c> and use the episode identifier, not
/// <see cref="SeriesId"/>.
/// </para>
/// </summary>
public sealed class SeriesSummary : CatalogItem
{
    [JsonPropertyName("series_id")]
    public int SeriesId { get; init; }

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

    /// <summary>
    /// Release date in camelCase. Xtream is inconsistent on this particular
    /// field: depending on the panel version it is named <c>releaseDate</c> or
    /// <c>release_date</c>. Read <see cref="ReleaseDate"/> rather than this
    /// property.
    /// </summary>
    [JsonPropertyName("releaseDate")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? ReleaseDateCamelCase { get; init; }

    /// <summary>snake_case variant of the previous field. See <see cref="ReleaseDate"/>.</summary>
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

    /// <summary>Backdrop images. A single string on some panels, an array on others.</summary>
    [JsonPropertyName("backdrop_path")]
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public IReadOnlyList<string> BackdropPaths { get; init; } = [];

    [JsonPropertyName("youtube_trailer")]
    public string? YoutubeTrailer { get; init; }

    /// <summary>Typical episode running time, in minutes.</summary>
    [JsonPropertyName("episode_run_time")]
    public int? EpisodeRunTime { get; init; }

    [JsonIgnore]
    public override StreamKind Kind => StreamKind.Series;

    [JsonIgnore]
    public override int Id => SeriesId;

    /// <summary>Release date, whichever field name the panel used.</summary>
    [JsonIgnore]
    public DateTimeOffset? ReleaseDate => ReleaseDateCamelCase ?? ReleaseDateSnakeCase;

    /// <summary>Title to display: <c>title</c> when present, <c>name</c> otherwise.</summary>
    [JsonIgnore]
    public string? DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Name : Title;
}
