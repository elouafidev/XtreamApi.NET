using System.Globalization;
using XtreamApi.Models;

namespace XtreamApi;

/// <summary>
/// Builds the playback URLs of the streams.
/// <para>
/// These URLs are not returned by the API: they are assembled from the
/// credentials and the item to play. They are the only thing a video player
/// needs.
/// </para>
/// </summary>
public sealed class XtreamStreamUrlBuilder
{
    /// <summary>Live container used when the panel says nothing else.</summary>
    public const string DefaultLiveFormat = "ts";

    /// <summary>Fallback VOD container, when the item declares none.</summary>
    public const string DefaultVodFormat = "mp4";

    private readonly Uri _baseAddress;
    private readonly string _username;
    private readonly string _password;

    private XtreamStreamUrlBuilder(Uri baseAddress, string username, string password)
    {
        _baseAddress = baseAddress;
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Creates a URL builder.
    /// </summary>
    /// <param name="credentials">Access credentials.</param>
    /// <param name="serverInfo">
    /// The <c>server_info</c> block obtained at sign-in. When supplied, its
    /// address wins: several providers stream from a host distinct from the one
    /// serving the API, and URLs built on the API address are then rejected.
    /// </param>
    public static XtreamStreamUrlBuilder For(XtreamCredentials credentials, ServerInfo? serverInfo = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return new XtreamStreamUrlBuilder(
            serverInfo?.BaseAddress ?? credentials.BaseAddress,
            credentials.Username,
            credentials.Password);
    }

    /// <summary>Address the streams are served from.</summary>
    public Uri BaseAddress => _baseAddress;

    /// <summary>
    /// URL of a live channel.
    /// </summary>
    /// <param name="streamId">Identifier of the channel.</param>
    /// <param name="format">
    /// Container wanted. Must appear in the account's
    /// <c>allowed_output_formats</c>: <c>ts</c> for a continuous stream,
    /// <c>m3u8</c> for HLS.
    /// </param>
    public Uri BuildLive(int streamId, string format = DefaultLiveFormat) =>
        Build("live", streamId, format);

    /// <summary>
    /// URL of a live channel.
    /// <para>
    /// When the provider imposes an address through <c>direct_source</c>, it is
    /// returned as-is.
    /// </para>
    /// </summary>
    public Uri BuildLive(LiveStream channel, string format = DefaultLiveFormat)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return TryDirectSource(channel) ?? BuildLive(channel.StreamId, format);
    }

    /// <summary>URL of a movie.</summary>
    public Uri BuildMovie(int streamId, string? containerExtension) =>
        Build("movie", streamId, Fallback(containerExtension, DefaultVodFormat));

    /// <summary>URL of a movie from the catalogue.</summary>
    public Uri BuildMovie(VodStream movie)
    {
        ArgumentNullException.ThrowIfNull(movie);

        return TryDirectSource(movie) ?? BuildMovie(movie.StreamId, movie.ContainerExtension);
    }

    /// <summary>URL of a movie from its detailed record.</summary>
    public Uri BuildMovie(VodMovieData movie)
    {
        ArgumentNullException.ThrowIfNull(movie);

        return TryDirectSource(movie) ?? BuildMovie(movie.StreamId, movie.ContainerExtension);
    }

    /// <summary>URL of a series episode.</summary>
    public Uri BuildEpisode(int episodeId, string? containerExtension) =>
        Build("series", episodeId, Fallback(containerExtension, DefaultVodFormat));

    /// <summary>
    /// URL of an episode.
    /// <para>
    /// The identifier used is the episode's, never the series'.
    /// </para>
    /// </summary>
    public Uri BuildEpisode(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        return TryDirectSource(episode) ?? BuildEpisode(episode.EpisodeId, episode.ContainerExtension);
    }

    /// <summary>
    /// URL of any catalogue item, according to its kind.
    /// </summary>
    public Uri BuildFor(CatalogItem item, string liveFormat = DefaultLiveFormat)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item switch
        {
            LiveStream channel => BuildLive(channel, liveFormat),
            VodStream movie => BuildMovie(movie),
            VodMovieData movie => BuildMovie(movie),
            Episode episode => BuildEpisode(episode),
            _ => TryDirectSource(item) ?? Build(SegmentFor(item.Kind), item.Id, liveFormat),
        };
    }

    /// <summary>
    /// Catch-up URL of a programme that has already aired.
    /// </summary>
    /// <param name="startInServerTime">
    /// Start of the programme, <b>expressed in the server's time zone</b>.
    /// Panels do not read this value as UTC: passing a UTC time to a server in
    /// Paris shifts playback by one or two hours.
    /// <see cref="ServerInfo.TimeZone"/> gives the zone to use.
    /// </param>
    /// <param name="streamId">Identifier of the channel to replay.</param>
    /// <param name="duration">How much to replay, rounded to the minute.</param>
    /// <param name="format">Container wanted, as for live streams.</param>
    public Uri BuildCatchup(
        int streamId,
        DateTimeOffset startInServerTime,
        TimeSpan duration,
        string format = DefaultLiveFormat)
    {
        var minutes = (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
        if (minutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "The catch-up duration must be at least one minute.");
        }

        // Layout imposed by the panels: yyyy-MM-dd:HH-mm.
        var start = startInServerTime.ToString("yyyy-MM-dd:HH-mm", CultureInfo.InvariantCulture);

        var path =
            $"timeshift/{Escape(_username)}/{Escape(_password)}/"
            + $"{minutes.ToString(CultureInfo.InvariantCulture)}/{Escape(start)}/"
            + $"{streamId.ToString(CultureInfo.InvariantCulture)}.{Escape(NormalizeFormat(format))}";

        return new Uri(_baseAddress, path);
    }

    private Uri Build(string segment, int id, string format)
    {
        var path =
            $"{segment}/{Escape(_username)}/{Escape(_password)}/"
            + $"{id.ToString(CultureInfo.InvariantCulture)}.{Escape(NormalizeFormat(format))}";

        return new Uri(_baseAddress, path);
    }

    private static string SegmentFor(StreamKind kind) => kind switch
    {
        StreamKind.Live => "live",
        StreamKind.Movie => "movie",
        StreamKind.Series => "series",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown stream kind."),
    };

    /// <summary>
    /// Returns the address imposed by the provider, when it is usable.
    /// </summary>
    private static Uri? TryDirectSource(CatalogItem item) =>
        !string.IsNullOrWhiteSpace(item.DirectSource)
        && Uri.TryCreate(item.DirectSource.Trim(), UriKind.Absolute, out var direct)
        && (direct.Scheme == Uri.UriSchemeHttp || direct.Scheme == Uri.UriSchemeHttps)
            ? direct
            : null;

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>
    /// Strips the leading dot that some panels leave in
    /// <c>container_extension</c>.
    /// </summary>
    private static string NormalizeFormat(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        return format.Trim().TrimStart('.');
    }

    /// <summary>
    /// Escapes a path segment. A password containing <c>/</c>, <c>?</c> or
    /// <c>#</c> would otherwise point the URL somewhere else.
    /// </summary>
    private static string Escape(string segment) => Uri.EscapeDataString(segment);
}
