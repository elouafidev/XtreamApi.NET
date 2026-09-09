using XtreamApi.Models;

namespace XtreamApi;

/// <summary>
/// Client for an Xtream panel.
/// <para>
/// The client owns the credentials: they are supplied once at construction and
/// appear in no method signature. Passing them on every call clutters every
/// caller and opens the door to mixing accounts up.
/// </para>
/// <para>
/// Every method reads; none of them writes to the panel.
/// </para>
/// </summary>
public interface IXtreamClient : IDisposable
{
    /// <summary>Credentials used by this client.</summary>
    XtreamCredentials Credentials { get; }

    /// <summary>
    /// Playback URL builder based on the credentials alone.
    /// <para>
    /// Once <c>server_info</c> is known, prefer
    /// <see cref="CreateStreamUrlBuilder"/>: some providers stream from a host
    /// different from the one serving the API.
    /// </para>
    /// </summary>
    XtreamStreamUrlBuilder StreamUrls { get; }

    /// <summary>
    /// Playback URL builder that honours the streaming host declared by the
    /// panel.
    /// </summary>
    XtreamStreamUrlBuilder CreateStreamUrlBuilder(ServerInfo? serverInfo);

    /// <summary>
    /// State of the account and of the server, reported as-is.
    /// <para>
    /// Does not throw when the credentials are rejected: the response then
    /// carries <c>auth = 0</c>, which a user interface may want to show. To
    /// validate a connection, use <see cref="AuthenticateAsync"/>.
    /// </para>
    /// </summary>
    Task<XtreamAccount> GetAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the connection and returns the state of the account.
    /// </summary>
    /// <exception cref="XtreamAuthenticationException">
    /// Credentials rejected, or account expired, disabled or banned.
    /// </exception>
    Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>Live categories.</summary>
    Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>VOD categories.</summary>
    Task<IReadOnlyList<XtreamCategory>> GetVodCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Series categories.</summary>
    Task<IReadOnlyList<XtreamCategory>> GetSeriesCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Live channels, optionally filtered by category.
    /// <para>
    /// Unfiltered, the response from a large provider routinely exceeds tens of
    /// megabytes: prefer <see cref="StreamLiveStreamsAsync"/> when the list is
    /// meant to fill a user interface as it arrives.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<LiveStream>> GetLiveStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Movies, optionally filtered by category.</summary>
    Task<IReadOnlyList<VodStream>> GetVodStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Series, optionally filtered by category.</summary>
    Task<IReadOnlyList<SeriesSummary>> GetSeriesAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Live channels yielded as they arrive.</summary>
    IAsyncEnumerable<LiveStream> StreamLiveStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Movies yielded as they arrive.</summary>
    IAsyncEnumerable<VodStream> StreamVodStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Series yielded as they arrive.</summary>
    IAsyncEnumerable<SeriesSummary> StreamSeriesAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detailed record of a movie. <c>null</c> when the panel does not know the
    /// identifier.
    /// </summary>
    Task<VodInfo?> GetVodInfoAsync(int vodId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detailed record of a series, with its seasons and episodes.
    /// <c>null</c> when the panel does not know the identifier.
    /// </summary>
    Task<SeriesInfo?> GetSeriesInfoAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upcoming programmes on a channel.
    /// </summary>
    /// <param name="streamId">Identifier of the channel whose guide is wanted.</param>
    /// <param name="limit">How many programmes to return. The panel decides when unspecified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<EpgListing>> GetShortEpgAsync(
        int streamId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full guide for one channel (<c>get_simple_data_table</c>).
    /// <para>
    /// This action requires a stream identifier. Calling it without one, as
    /// some clients do, never returns anything.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EpgListing>> GetEpgAsync(int streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Programmes available through catch-up on a channel.
    /// <para>
    /// Support for this action, and the interpretation of its bounds, vary from
    /// panel to panel: an empty list does not always mean catch-up is
    /// unavailable. <see cref="LiveStream.HasCatchup"/> remains the more
    /// reliable signal.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EpgListing>> GetCatchupTableAsync(
        int streamId,
        DateTimeOffset? startInServerTime = null,
        DateTimeOffset? endInServerTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the full guide in XMLTV form.
    /// <para>
    /// Returns XML, not JSON, and can exceed a hundred megabytes: parse the
    /// stream as it is read, and dispose of it in the caller.
    /// </para>
    /// </summary>
    Task<Stream> OpenXmltvAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the account's M3U playlist. The caller disposes of the stream.
    /// </summary>
    Task<Stream> OpenPlaylistAsync(
        string type = "m3u_plus",
        string output = "ts",
        CancellationToken cancellationToken = default);
}
