using System.Net;
using System.Net.Http.Headers;

namespace XtreamApi.Http;

/// <summary>
/// Builds an <see cref="HttpClient"/> tuned for Xtream panels.
/// </summary>
public static class XtreamHttpClientFactory
{
    /// <summary>
    /// Creates a ready-to-use client.
    /// <para>
    /// The returned instance is meant to be shared and to live as long as the
    /// application. Creating one per call exhausts sockets even with a correct
    /// <c>Dispose</c>: the connections linger in TIME_WAIT.
    /// </para>
    /// </summary>
    public static HttpClient Create(XtreamClientOptions? options = null)
    {
        options ??= new XtreamClientOptions();

        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = options.ConnectTimeout,

            // Recycle connections so DNS changes are picked up: panels change
            // address without warning.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = options.MaxConnectionsPerServer,
        };

        if (options.AllowInvalidCertificates)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        var client = new HttpClient(handler, disposeHandler: true)
        {
            // Timeouts are handled per request, separating the wait for headers
            // from reading the body. A global timeout here would cut off the
            // transfer of large responses.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        // TryAddWithoutValidation rather than UserAgent.ParseAdd: the strings
        // some providers require do not follow header grammar and would be
        // rejected at construction.
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        return client;
    }
}
