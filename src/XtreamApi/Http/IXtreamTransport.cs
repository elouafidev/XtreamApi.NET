namespace XtreamApi.Http;

/// <summary>
/// Carries the calls to an Xtream panel.
/// <para>
/// Kept separate from the high-level client so it can be replaced in tests
/// without a server, and so the network policy (timeouts, retries, headers)
/// lives in one place.
/// </para>
/// </summary>
public interface IXtreamTransport : IDisposable
{
    /// <summary>
    /// Reports how much of a JSON response has arrived.
    /// <para>
    /// Raised on the thread reading the response, never on the caller's: a
    /// subscriber that touches a user interface must marshal back to it itself.
    /// </para>
    /// <para>
    /// The event does not distinguish between calls. Two requests running side
    /// by side would therefore see their progress interleave.
    /// </para>
    /// </summary>
    event EventHandler<XtreamProgress>? Progress;

    /// <summary>
    /// Performs a call and deserialises the JSON response.
    /// </summary>
    /// <exception cref="XtreamConnectionException">Server unreachable or timed out.</exception>
    /// <exception cref="XtreamAuthenticationException">Credentials rejected.</exception>
    /// <exception cref="XtreamHttpException">Error status returned by the server.</exception>
    /// <exception cref="XtreamProtocolException">Response unreadable or malformed.</exception>
    Task<T?> GetJsonAsync<T>(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a call whose response is a JSON array, yielding its items as
    /// they arrive.
    /// <para>
    /// Always preferable for catalogues: <c>get_live_streams</c> routinely
    /// exceeds tens of megabytes, which this method never holds in memory all at
    /// once.
    /// </para>
    /// </summary>
    IAsyncEnumerable<T> StreamJsonArrayAsync<T>(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a call and returns the raw response as text. Intended for the
    /// endpoints that do not return JSON (<c>xmltv.php</c>, <c>get.php</c>).
    /// </summary>
    Task<string> GetTextAsync(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the response for reading; the caller disposes of the stream.
    /// <para>
    /// The only sensible route for <c>xmltv.php</c>, which can exceed a hundred
    /// megabytes and must be parsed as a stream.
    /// </para>
    /// </summary>
    Task<Stream> OpenReadAsync(
        XtreamCredentials credentials,
        XtreamRequest request,
        CancellationToken cancellationToken = default);
}
