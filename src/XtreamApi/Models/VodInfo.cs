using System.Text.Json;
using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// Response of <c>get_vod_info</c>: the detailed record of a movie.
/// </summary>
public sealed class VodInfo
{
    [JsonPropertyName("info")]
    public VodDetails? Details { get; init; }

    [JsonPropertyName("movie_data")]
    public VodMovieData? MovieData { get; init; }
}

/// <summary>
/// The <c>info</c> block of <c>get_vod_info</c>: the editorial metadata.
/// </summary>
public sealed class VodDetails
{
    [JsonPropertyName("tmdb_id")]
    public string? TmdbId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Original title.</summary>
    [JsonPropertyName("o_name")]
    public string? OriginalName { get; init; }

    [JsonPropertyName("cover_big")]
    public string? CoverBigUrl { get; init; }

    [JsonPropertyName("movie_image")]
    public string? ImageUrl { get; init; }

    /// <summary>Note: this field is lowercase with no separator, unlike the series one.</summary>
    [JsonPropertyName("releasedate")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? ReleaseDate { get; init; }

    [JsonPropertyName("youtube_trailer")]
    public string? YoutubeTrailer { get; init; }

    [JsonPropertyName("director")]
    public string? Director { get; init; }

    [JsonPropertyName("actors")]
    public string? Actors { get; init; }

    [JsonPropertyName("cast")]
    public string? Cast { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonPropertyName("age")]
    public string? Age { get; init; }

    [JsonPropertyName("mpaa_rating")]
    public string? MpaaRating { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("genre")]
    public string? Genre { get; init; }

    [JsonPropertyName("backdrop_path")]
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public IReadOnlyList<string> BackdropPaths { get; init; } = [];

    [JsonPropertyName("duration_secs")]
    public int? DurationSeconds { get; init; }

    /// <summary>Human-readable duration, in <c>hh:mm:ss</c> form.</summary>
    [JsonPropertyName("duration")]
    public string? DurationText { get; init; }

    [JsonPropertyName("bitrate")]
    public int? Bitrate { get; init; }

    [JsonPropertyName("rating")]
    public double? Rating { get; init; }

    /// <summary>
    /// Raw ffprobe output for the video track. The shape varies from panel to
    /// panel and has no stable schema, so it is left as-is rather than modelled
    /// incorrectly.
    /// </summary>
    [JsonPropertyName("video")]
    public JsonElement? Video { get; init; }

    /// <summary>Raw ffprobe output for the audio track. See <see cref="Video"/>.</summary>
    [JsonPropertyName("audio")]
    public JsonElement? Audio { get; init; }

    /// <summary>Usable duration, rebuilt from <see cref="DurationSeconds"/>.</summary>
    [JsonIgnore]
    public TimeSpan? Duration =>
        DurationSeconds is > 0 ? TimeSpan.FromSeconds(DurationSeconds.Value) : null;

    /// <summary>Synopsis: <c>plot</c> when present, <c>description</c> otherwise.</summary>
    [JsonIgnore]
    public string? Synopsis => string.IsNullOrWhiteSpace(Plot) ? Description : Plot;
}

/// <summary>
/// The <c>movie_data</c> block of <c>get_vod_info</c>: what is needed to play the movie.
/// </summary>
public sealed class VodMovieData : CatalogItem
{
    [JsonPropertyName("stream_id")]
    public int StreamId { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("container_extension")]
    public string? ContainerExtension { get; init; }

    [JsonIgnore]
    public override StreamKind Kind => StreamKind.Movie;

    [JsonIgnore]
    public override int Id => StreamId;
}
