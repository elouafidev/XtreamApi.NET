using System.Text.Json.Serialization;

namespace XtreamApi.Models;

/// <summary>
/// A live channel, as returned by <c>get_live_streams</c>.
/// </summary>
public sealed class LiveStream : CatalogItem
{
    [JsonPropertyName("stream_id")]
    public int StreamId { get; init; }

    /// <summary>Always "live" in practice, sometimes "radio_streams".</summary>
    [JsonPropertyName("stream_type")]
    public string? StreamType { get; init; }

    [JsonPropertyName("stream_icon")]
    public string? LogoUrl { get; init; }

    /// <summary>
    /// Key used to match the channel against the XMLTV guide. Often empty: a
    /// channel without it has no programme to display.
    /// </summary>
    [JsonPropertyName("epg_channel_id")]
    public string? EpgChannelId { get; init; }

    [JsonPropertyName("is_adult")]
    public bool IsAdult { get; init; }

    /// <summary>Catch-up is enabled on this channel.</summary>
    [JsonPropertyName("tv_archive")]
    public bool TvArchive { get; init; }

    /// <summary>Catch-up depth, in days.</summary>
    [JsonPropertyName("tv_archive_duration")]
    public int TvArchiveDuration { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonIgnore]
    public override StreamKind Kind => StreamKind.Live;

    [JsonIgnore]
    public override int Id => StreamId;

    /// <summary>
    /// Catch-up is actually usable: panels sometimes set <c>tv_archive</c>
    /// without providing any depth.
    /// </summary>
    [JsonIgnore]
    public bool HasCatchup => TvArchive && TvArchiveDuration > 0;

    /// <summary>The programme guide can be matched to this channel.</summary>
    [JsonIgnore]
    public bool HasEpg => !string.IsNullOrWhiteSpace(EpgChannelId);
}
