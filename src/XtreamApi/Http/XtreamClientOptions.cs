namespace XtreamApi.Http;

/// <summary>
/// Settings of the HTTP transport.
/// </summary>
public sealed class XtreamClientOptions
{
    /// <summary>
    /// The <c>User-Agent</c> header sent on every call.
    /// <para>
    /// Many panels answer 401 to a request without a User-Agent, or accept only
    /// a list of known agents. This is the first thing to change when a provider
    /// rejects an otherwise valid connection.
    /// </para>
    /// </summary>
    public string UserAgent { get; init; } = "XtreamApi/1.0";

    /// <summary>Timeout for establishing the TCP and TLS connection.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Timeout for the response headers.
    /// <para>
    /// This timeout deliberately does not cover reading the body: a
    /// <c>get_live_streams</c> response routinely weighs tens of megabytes, and
    /// a global timeout would cut it off mid-transfer. Reading the body is
    /// bounded only by the caller's cancellation token.
    /// </para>
    /// </summary>
    public TimeSpan ResponseHeadersTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many retries after a transient failure: network outage, timeout,
    /// 5xx or 429. Never on a credential refusal.
    /// </summary>
    public int RetryCount { get; init; } = 2;

    /// <summary>Delay before the first retry, doubled thereafter.</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum number of simultaneous connections to a single panel.
    /// <para>
    /// API calls do not normally consume the subscription's connection quota, as
    /// streams do, but some panels count everything.
    /// </para>
    /// </summary>
    public int MaxConnectionsPerServer { get; init; } = 8;

    /// <summary>
    /// How much of the response start is kept in error messages.
    /// </summary>
    public int ErrorSnippetLength { get; init; } = 512;

    /// <summary>
    /// Accepts invalid TLS certificates.
    /// <para>
    /// Only to be enabled at the user's explicit request: it disables
    /// verification of the server's identity and exposes the connection to
    /// interception. Many panels unfortunately use self-signed or expired
    /// certificates.
    /// </para>
    /// </summary>
    public bool AllowInvalidCertificates { get; init; }
}
