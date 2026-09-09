using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// Envelope of the EPG responses: <c>get_short_epg</c>,
/// <c>get_simple_data_table</c> and <c>get_catchup_table</c> all return an
/// object containing <c>epg_listings</c>, never a bare array.
/// </summary>
public sealed class EpgResponse
{
    [JsonPropertyName("epg_listings")]
    [JsonConverter(typeof(FlexibleListConverter<EpgListing>))]
    public IReadOnlyList<EpgListing> Listings { get; init; } = [];
}

/// <summary>
/// A programme from the electronic programme guide.
/// </summary>
public sealed class EpgListing
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("epg_id")]
    public long? EpgId { get; init; }

    /// <summary>Programme title. Base64-encoded by the panel, decoded on read.</summary>
    [JsonPropertyName("title")]
    [JsonConverter(typeof(Base64TextConverter))]
    public string? Title { get; init; }

    /// <summary>Synopsis. Base64-encoded by the panel, decoded on read.</summary>
    [JsonPropertyName("description")]
    [JsonConverter(typeof(Base64TextConverter))]
    public string? Description { get; init; }

    [JsonPropertyName("lang")]
    public string? Language { get; init; }

    /// <summary>
    /// Start time as text, expressed in the server's time zone.
    /// Prefer <see cref="StartsAt"/>.
    /// </summary>
    [JsonPropertyName("start")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? StartLocal { get; init; }

    /// <summary>
    /// End time as text, expressed in the server's time zone.
    /// Prefer <see cref="EndsAt"/>.
    /// </summary>
    [JsonPropertyName("end")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? EndLocal { get; init; }

    [JsonPropertyName("start_timestamp")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTimeOffset? StartTimestamp { get; init; }

    [JsonPropertyName("stop_timestamp")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTimeOffset? StopTimestamp { get; init; }

    /// <summary>XMLTV identifier of the channel, to be matched against <c>epg_channel_id</c>.</summary>
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; init; }

    [JsonPropertyName("now_playing")]
    public bool NowPlaying { get; init; }

    /// <summary>This programme is available through catch-up.</summary>
    [JsonPropertyName("has_archive")]
    public bool HasArchive { get; init; }

    /// <summary>
    /// Start time in UTC. The epoch timestamp is authoritative; the text field,
    /// expressed in the server's time zone, is only a fallback.
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset? StartsAt => StartTimestamp ?? StartLocal;

    /// <summary>End time in UTC. See <see cref="StartsAt"/>.</summary>
    [JsonIgnore]
    public DateTimeOffset? EndsAt => StopTimestamp ?? EndLocal;

    /// <summary>Programme duration, <c>null</c> when the bounds are incomplete.</summary>
    [JsonIgnore]
    public TimeSpan? Duration =>
        StartsAt is { } start && EndsAt is { } end && end > start ? end - start : null;

    /// <summary>The programme is on air right now.</summary>
    [JsonIgnore]
    public bool IsOnAir =>
        StartsAt is { } start && EndsAt is { } end
        && DateTimeOffset.UtcNow >= start && DateTimeOffset.UtcNow < end;
}
