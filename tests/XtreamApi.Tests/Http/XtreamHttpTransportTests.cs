using System.Net;
using XtreamApi.Http;
using XtreamApi.Models;

namespace XtreamApi.Tests.Http;

public class XtreamHttpTransportTests
{
    private static readonly XtreamCredentials Credentials =
        XtreamCredentials.Create("http://panel.example.com:8080", "demo", "secret");

    /// <summary>Fast options: tests must not wait.</summary>
    private static XtreamClientOptions FastOptions(int retryCount = 0) => new()
    {
        RetryCount = retryCount,
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        ResponseHeadersTimeout = TimeSpan.FromMilliseconds(200),
    };

    private static XtreamHttpTransport CreateTransport(
        StubHttpMessageHandler handler,
        XtreamClientOptions? options = null) =>
        new(handler.CreateClient(), options ?? FastOptions());

    [Fact]
    public async Task Deserialises_a_json_response()
    {
        using var handler = StubHttpMessageHandler.Responding(
            """{"user_info":{"auth":1,"status":"Active"},"server_info":{"url":"panel.example.com"}}""");
        using var transport = CreateTransport(handler);

        var account = await transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account());

        Assert.True(account?.User?.IsAuthenticated);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void The_factory_sets_the_configured_user_agent()
    {
        // The first remedy when a panel rejects a valid connection.
        using var httpClient = XtreamHttpClientFactory.Create(
            new XtreamClientOptions { UserAgent = "VLC/3.0.20 LibVLC/3.0.20" });

        Assert.Equal("VLC/3.0.20 LibVLC/3.0.20", httpClient.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public void The_factory_leaves_the_client_timeout_infinite()
    {
        // A global timeout would cut off a large catalogue mid-transfer:
        // timeouts are handled per phase inside the transport.
        using var httpClient = XtreamHttpClientFactory.Create();

        Assert.Equal(Timeout.InfiniteTimeSpan, httpClient.Timeout);
    }

    [Fact]
    public async Task The_client_user_agent_travels_with_every_call()
    {
        using var handler = StubHttpMessageHandler.Responding("{}");
        var httpClient = handler.CreateClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "XtreamApi/1.0");

        using var transport = new XtreamHttpTransport(httpClient, FastOptions());
        await transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account());

        Assert.Equal("XtreamApi/1.0", handler.LastHeaders?.UserAgent.ToString());
        httpClient.Dispose();
    }

    [Fact]
    public async Task Yields_the_items_of_an_array_as_they_are_read()
    {
        using var handler = StubHttpMessageHandler.Responding(
            """[{"stream_id":1,"name":"A"},{"stream_id":2,"name":"B"},{"stream_id":3,"name":"C"}]""");
        using var transport = CreateTransport(handler);

        var names = new List<string?>();
        await foreach (var channel in transport.StreamJsonArrayAsync<LiveStream>(
            Credentials,
            XtreamRequest.ForAction("get_live_streams")))
        {
            names.Add(channel.Name);
        }

        Assert.Equal(["A", "B", "C"], names);
    }

    [Fact]
    public async Task Enumerating_a_large_catalogue_stops_on_cancellation()
    {
        // A real catalogue runs to tens of thousands of items and tens of
        // megabytes. The user must be able to interrupt the load without waiting
        // for the transfer to finish.
        var channels = string.Join(',', Enumerable.Range(1, 20_000)
            .Select(index => $$"""{"stream_id":{{index}},"name":"Channel {{index}}"}"""));

        using var handler = StubHttpMessageHandler.Responding($"[{channels}]");
        using var transport = CreateTransport(handler);
        using var cancellation = new CancellationTokenSource();

        var seen = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in transport.StreamJsonArrayAsync<LiveStream>(
                Credentials,
                XtreamRequest.ForAction("get_live_streams"),
                cancellation.Token))
            {
                seen++;
                await cancellation.CancelAsync();
            }
        });

        // The exact count depends on the read buffer sizes; what matters is that
        // the enumeration stopped well before the end.
        Assert.InRange(seen, 1, 19_999);
    }

    [Fact]
    public async Task A_credential_refusal_is_reported_as_such_and_is_not_retried()
    {
        using var handler = StubHttpMessageHandler.Failing(HttpStatusCode.Unauthorized);
        using var transport = CreateTransport(handler, FastOptions(retryCount: 3));

        var exception = await Assert.ThrowsAsync<XtreamAuthenticationException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);

        // Hammering a panel that refuses gets the address banned at some
        // providers: a single attempt.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task An_unreachable_server_is_distinguished_from_a_credential_refusal()
    {
        using var handler = StubHttpMessageHandler.Unreachable();
        using var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<XtreamConnectionException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.False(exception.IsTimeout);

        // The address quoted in the error must not leak the password.
        Assert.DoesNotContain("secret", exception.RequestUri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_silent_panel_produces_a_timeout_error()
    {
        using var handler = StubHttpMessageHandler.Hanging();
        using var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<XtreamConnectionException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.True(exception.IsTimeout);
    }

    [Fact]
    public async Task Cancellation_requested_by_the_caller_is_not_turned_into_a_timeout()
    {
        using var handler = StubHttpMessageHandler.Hanging();
        using var transport = CreateTransport(
            handler,
            new XtreamClientOptions { RetryCount = 0, ResponseHeadersTimeout = TimeSpan.FromMinutes(5) });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account(), cancellation.Token));
    }

    [Fact]
    public async Task A_transient_error_is_retried_then_reported()
    {
        using var handler = StubHttpMessageHandler.Failing(HttpStatusCode.BadGateway);
        using var transport = CreateTransport(handler, FastOptions(retryCount: 2));

        var exception = await Assert.ThrowsAsync<XtreamHttpException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.True(exception.IsTransient);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task A_permanent_error_is_not_retried()
    {
        using var handler = StubHttpMessageHandler.Failing(HttpStatusCode.NotFound);
        using var transport = CreateTransport(handler, FastOptions(retryCount: 2));

        var exception = await Assert.ThrowsAsync<XtreamHttpException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.False(exception.IsTransient);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_transient_error_followed_by_success_returns_the_response()
    {
        using var handler = StubHttpMessageHandler.Sequence(
            StubHttpMessageHandler.Build(HttpStatusCode.ServiceUnavailable, "", "text/html"),
            StubHttpMessageHandler.Build(HttpStatusCode.OK, """{"user_info":{"auth":1}}""", "application/json"));
        using var transport = CreateTransport(handler, FastOptions(retryCount: 2));

        var account = await transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account());

        Assert.True(account?.User?.IsAuthenticated);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task An_html_page_returned_as_200_is_reported_with_its_content()
    {
        // The most frequent case in production: the panel answers 200 with an
        // error page. Without the body the incident cannot be diagnosed.
        const string body = "<html><body><h1>403 Forbidden</h1><p>nginx</p></body></html>";
        using var handler = StubHttpMessageHandler.Responding(body, "text/html");
        using var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<XtreamProtocolException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.Contains("HTML", exception.Message, StringComparison.Ordinal);
        Assert.Contains("403 Forbidden", exception.ResponseSnippet ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_non_json_text_response_is_reported_with_its_content()
    {
        using var handler = StubHttpMessageHandler.Responding("Not Found", "text/plain");
        using var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<XtreamProtocolException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.Equal("Not Found", exception.ResponseSnippet);
    }

    [Fact]
    public async Task An_empty_response_is_reported()
    {
        using var handler = StubHttpMessageHandler.Responding("", "application/json");
        using var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<XtreamProtocolException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.Contains("empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Truncated_json_is_reported_with_the_start_of_the_response()
    {
        using var handler = StubHttpMessageHandler.Responding("""{"user_info":{"auth":1,""");
        using var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<XtreamProtocolException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));

        Assert.Contains("user_info", exception.ResponseSnippet ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_large_response_is_read_in_full_despite_the_leading_inspection()
    {
        // The inspection consumes only the leading bytes: the rest must keep
        // feeding the deserialiser with nothing lost.
        var channels = string.Join(',', Enumerable.Range(1, 2000)
            .Select(index => $$"""{"stream_id":{{index}},"name":"Channel {{index}}"}"""));

        using var handler = StubHttpMessageHandler.Responding($"[{channels}]");
        using var transport = CreateTransport(handler);

        var count = 0;
        var lastId = 0;

        await foreach (var channel in transport.StreamJsonArrayAsync<LiveStream>(
            Credentials,
            XtreamRequest.ForAction("get_live_streams")))
        {
            count++;
            lastId = channel.StreamId;
        }

        Assert.Equal(2000, count);
        Assert.Equal(2000, lastId);
    }

    [Fact]
    public async Task The_xml_of_xmltv_is_not_subjected_to_the_json_check()
    {
        const string body = """<?xml version="1.0"?><tv><channel id="France2.fr" /></tv>""";
        using var handler = StubHttpMessageHandler.Responding(body, "text/xml");
        using var transport = CreateTransport(handler);

        var text = await transport.GetTextAsync(Credentials, XtreamRequest.Xmltv());

        Assert.Equal(body, text);
    }

    [Fact]
    public async Task The_opened_stream_stays_readable_after_the_call()
    {
        const string body = """<?xml version="1.0"?><tv></tv>""";
        using var handler = StubHttpMessageHandler.Responding(body, "text/xml");
        using var transport = CreateTransport(handler);

        var stream = await transport.OpenReadAsync(Credentials, XtreamRequest.Xmltv());

        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream);
            Assert.Equal(body, await reader.ReadToEndAsync());
        }
    }

    [Fact]
    public async Task A_disposed_transport_refuses_calls()
    {
        using var handler = StubHttpMessageHandler.Responding("{}");
        var transport = CreateTransport(handler);
        transport.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account()));
    }

    [Fact]
    public async Task A_supplied_HttpClient_is_not_disposed_by_the_transport()
    {
        // The client may be shared by the whole application: disposing it from
        // the transport would break the other calls.
        using var handler = StubHttpMessageHandler.Responding("""{"user_info":{"auth":1}}""");
        var httpClient = handler.CreateClient();

        using (new XtreamHttpTransport(httpClient, FastOptions()))
        {
        }

        // The client must stay usable after the transport is disposed.
        using var second = new XtreamHttpTransport(httpClient, FastOptions());
        var account = await second.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account());

        Assert.True(account?.User?.IsAuthenticated);
        httpClient.Dispose();
    }
}
