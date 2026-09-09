using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// A movie from the VOD catalogue, as returned by <c>get_vod_streams</c>.
/// </summary>
public sealed class VodStream : CatalogItem
{
    [JsonPropertyName("stream_id")]
    public int StreamId { get; init; }

    /// <summary>Always "movie" in practice.</summary>
    [JsonPropertyName("stream_type")]
    public string? StreamType { get; init; }

    [JsonPropertyName("stream_icon")]
    public string? PosterUrl { get; init; }

    /// <summary>Cleaned-up title, when the panel distinguishes it from <c>name</c>.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    /// <summary>Rating out of 10. May arrive as <c>null</c> or an empty string.</summary>
    [JsonPropertyName("rating")]
    public double? Rating { get; init; }

    /// <summary>The same rating scaled to 5.</summary>
    [JsonPropertyName("rating_5based")]
    public double? Rating5Based { get; init; }

    /// <summary>
    /// File container (<c>mp4</c>, <c>mkv</c>, <c>avi</c>). Required to build the
    /// playback URL: unlike live streams, the extension is not free to choose.
    /// </summary>
    [JsonPropertyName("container_extension")]
    public string? ContainerExtension { get; init; }

    [JsonPropertyName("tmdb_id")]
    public string? TmdbId { get; init; }

    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonIgnore]
    public override StreamKind Kind => StreamKind.Movie;

    [JsonIgnore]
    public override int Id => StreamId;

    /// <summary>Title to display: <c>title</c> when present, <c>name</c> otherwise.</summary>
    [JsonIgnore]
    public string? DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Name : Title;
}
