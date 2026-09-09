using System.Net;
using System.Text;

namespace XtreamApi.Tests.Http;

/// <summary>
/// Stub HTTP handler: returns prepared responses and records the calls it
/// receives.
/// <para>
/// Testing the transport against non-existent URLs only checks .NET's ability to
/// fail. This stub makes it possible to observe what the transport sends and
/// what it does with what it gets back.
/// </para>
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;

    private StubHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
        => _responder = responder;

    /// <summary>URLs that were called, in order.</summary>
    public List<Uri> Requests { get; } = [];

    /// <summary>Headers of the last request received.</summary>
    public System.Net.Http.Headers.HttpRequestHeaders? LastHeaders { get; private set; }

    public int CallCount => Requests.Count;

    /// <summary>Always returns the same 200 response with the supplied body.</summary>
    public static StubHttpMessageHandler Responding(string body, string contentType = "application/json") =>
        new((_, _, _) => Task.FromResult(Build(HttpStatusCode.OK, body, contentType)));

    /// <summary>
    /// Returns a response whose length is not announced, as a server replying in
    /// chunks does, or one whose content .NET has decompressed.
    /// </summary>
    public static StubHttpMessageHandler RespondingWithoutLength(string body) =>
        new((_, _, _) =>
        {
            var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(body)));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            content.Headers.ContentLength = null;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

    /// <summary>Always returns the same status, with an optional body.</summary>
    public static StubHttpMessageHandler Failing(HttpStatusCode statusCode, string body = "") =>
        new((_, _, _) => Task.FromResult(Build(statusCode, body, "text/html")));

    /// <summary>
    /// Returns a different response on each call; the index starts at 1.
    /// </summary>
    public static StubHttpMessageHandler Sequence(params HttpResponseMessage[] responses) =>
        new((_, call, _) => Task.FromResult(responses[Math.Min(call, responses.Length) - 1]));

    /// <summary>Never answers, to exercise the timeout.</summary>
    public static StubHttpMessageHandler Hanging() =>
        new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return Build(HttpStatusCode.OK, "{}", "application/json");
        });

    /// <summary>Fails at the network level, like an unresolvable host.</summary>
    public static StubHttpMessageHandler Unreachable() =>
        new((_, _, _) => throw new HttpRequestException("Host not found."));

    public static HttpResponseMessage Build(HttpStatusCode statusCode, string body, string contentType)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType)
        {
            CharSet = "utf-8",
        };

        return new HttpResponseMessage(statusCode) { Content = content };
    }

    /// <summary>Ready-to-use client, configured like the real factory's.</summary>
    public HttpClient CreateClient() => new(this, disposeHandler: false)
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        LastHeaders = request.Headers;
        return _responder(request, Requests.Count, cancellationToken);
    }
}
