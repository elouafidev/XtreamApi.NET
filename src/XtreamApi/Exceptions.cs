using System.Net;

namespace XtreamApi;

/// <summary>
/// Base type of every Xtream-specific failure.
/// </summary>
public abstract class XtreamException : Exception
{
    protected XtreamException(string message)
        : base(message)
    {
    }

    protected XtreamException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The server could not be reached: DNS, TLS, timeout, network outage.
/// <para>
/// Carefully distinct from <see cref="XtreamAuthenticationException"/>: the user
/// needs to know whether to fix their credentials or check their connection.
/// Collapsing both into a single <c>HttpRequestException</c> with no response
/// body makes that impossible.
/// </para>
/// </summary>
public sealed class XtreamConnectionException : XtreamException
{
    public XtreamConnectionException(Uri requestUri, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        RequestUri = requestUri;
    }

    /// <summary>The address that was called, with the password masked.</summary>
    public Uri RequestUri { get; }

    /// <summary>The wait timed out, as opposed to the network refusing outright.</summary>
    public bool IsTimeout { get; init; }
}

/// <summary>
/// The credentials were rejected: status 401 or 403, or <c>auth = 0</c> in the
/// panel's response.
/// </summary>
public sealed class XtreamAuthenticationException : XtreamException
{
    public XtreamAuthenticationException(string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>Status returned, <c>null</c> when the refusal came from the response body.</summary>
    public HttpStatusCode? StatusCode { get; }
}

/// <summary>
/// The server answered with an error status that is neither a credential
/// refusal nor a network failure.
/// </summary>
public sealed class XtreamHttpException : XtreamException
{
    public XtreamHttpException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// The failure may go away on the next attempt: panel overload (5xx) or
    /// rate limiting (429).
    /// </summary>
    public bool IsTransient =>
        StatusCode == HttpStatusCode.TooManyRequests || (int)StatusCode >= 500;
}

/// <summary>
/// The response arrived but is unusable: an HTML error page, XML where JSON was
/// expected, an empty body, malformed JSON.
/// <para>
/// Xtream panels very often answer 200 with an error page in the body. Without
/// the first bytes of that response the incident cannot be diagnosed, so
/// <see cref="ResponseSnippet"/> keeps them.
/// </para>
/// </summary>
public sealed class XtreamProtocolException : XtreamException
{
    public XtreamProtocolException(string message, string? responseSnippet = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ResponseSnippet = responseSnippet;
    }

    /// <summary>Start of the received body, truncated, for diagnosis.</summary>
    public string? ResponseSnippet { get; }
}

/// <summary>
/// The supplied URL is not a usable Xtream address.
/// </summary>
public sealed class XtreamUrlFormatException : XtreamException
{
    public XtreamUrlFormatException(string message)
        : base(message)
    {
    }
}
