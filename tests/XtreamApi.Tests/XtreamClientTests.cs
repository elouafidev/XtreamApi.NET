using XtreamApi.Http;
using XtreamApi.Tests.Http;

namespace XtreamApi.Tests;

public class XtreamClientTests
{
    private static readonly XtreamCredentials Credentials =
        XtreamCredentials.Create("http://panel.example.com:8080", "demo", "secret");

    private static XtreamClientOptions FastOptions() => new()
    {
        RetryCount = 0,
        ResponseHeadersTimeout = TimeSpan.FromSeconds(5),
    };

    private static (XtreamClient Client, StubHttpMessageHandler Handler) Create(string responseBody)
    {
        var handler = StubHttpMessageHandler.Responding(responseBody);
        var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());

        return (new XtreamClient(Credentials, transport, ownsTransport: true), handler);
    }

    private static string LastQuery(StubHttpMessageHandler handler) => handler.Requests[^1].Query;

    [Fact]
    public async Task AuthenticateAsync_accepts_an_active_account()
    {
        var (client, _) = Create("""{"user_info":{"auth":1,"status":"Active","max_connections":"2"}}""");

        using (client)
        {
            var account = await client.AuthenticateAsync();

            Assert.True(account.IsUsable);
            Assert.Equal(2, account.User?.MaxConnections);
        }
    }

    [Fact]
    public async Task AuthenticateAsync_rejects_refused_credentials()
    {
        var (client, _) = Create("""{"user_info":{"auth":0}}""");

        using (client)
        {
            var exception = await Assert.ThrowsAsync<XtreamAuthenticationException>(() => client.AuthenticateAsync());

            Assert.Contains("rejected", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AuthenticateAsync_rejects_an_expired_subscription_despite_auth_1()
    {
        // The panel accepts the credentials but the subscription is over:
        // without this check the connection looks fine, then every read fails.
        var (client, _) = Create("""{"user_info":{"auth":1,"status":"Expired","exp_date":"1600000000"}}""");

        using (client)
        {
            var exception = await Assert.ThrowsAsync<XtreamAuthenticationException>(() => client.AuthenticateAsync());

            Assert.Contains("expired", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("Disabled", "disabled")]
    [InlineData("Banned", "banned")]
    public async Task AuthenticateAsync_explains_why_the_account_is_unusable(string status, string expected)
    {
        var (client, _) = Create($$$"""{"user_info":{"auth":1,"status":"{{{status}}}"}}""");

        using (client)
        {
            var exception = await Assert.ThrowsAsync<XtreamAuthenticationException>(() => client.AuthenticateAsync());

            Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetAccountAsync_passes_no_judgement_on_the_account()
    {
        // A user interface must be able to show the real state without having to
        // catch an exception.
        var (client, _) = Create("""{"user_info":{"auth":0,"status":"Expired"}}""");

        using (client)
        {
            var account = await client.GetAccountAsync();

            Assert.False(account.IsUsable);
            Assert.False(account.User?.IsAuthenticated);
        }
    }

    [Fact]
    public async Task Categories_are_requested_with_their_action()
    {
        var (client, handler) = Create("""[{"category_id":"1","category_name":"Sport"}]""");

        using (client)
        {
            var categories = await client.GetLiveCategoriesAsync();

            Assert.Equal("Sport", Assert.Single(categories).Name);
            Assert.Contains("action=get_live_categories", LastQuery(handler), StringComparison.Ordinal);

            await client.GetVodCategoriesAsync();
            Assert.Contains("action=get_vod_categories", LastQuery(handler), StringComparison.Ordinal);

            await client.GetSeriesCategoriesAsync();
            Assert.Contains("action=get_series_categories", LastQuery(handler), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_live_catalogue_can_be_filtered_by_category()
    {
        var (client, handler) = Create("""[{"stream_id":1,"name":"A"}]""");

        using (client)
        {
            await client.GetLiveStreamsAsync();
            Assert.DoesNotContain("category_id", LastQuery(handler), StringComparison.Ordinal);

            await client.GetLiveStreamsAsync(categoryId: 7);
            Assert.Contains("category_id=7", LastQuery(handler), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Each_catalogue_targets_its_own_action()
    {
        var (client, handler) = Create("[]");

        using (client)
        {
            await client.GetLiveStreamsAsync();
            Assert.Contains("action=get_live_streams", LastQuery(handler), StringComparison.Ordinal);

            await client.GetVodStreamsAsync();
            Assert.Contains("action=get_vod_streams", LastQuery(handler), StringComparison.Ordinal);

            await client.GetSeriesAsync();
            Assert.Contains("action=get_series", LastQuery(handler), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_catalogue_can_be_walked_as_a_stream()
    {
        var (client, _) = Create("""[{"stream_id":1,"name":"A"},{"stream_id":2,"name":"B"}]""");

        using (client)
        {
            var names = new List<string?>();
            await foreach (var channel in client.StreamLiveStreamsAsync())
            {
                names.Add(channel.Name);
            }

            Assert.Equal(["A", "B"], names);
        }
    }

    [Fact]
    public async Task A_series_record_is_read_with_its_episodes()
    {
        var (client, handler) = Create(
            """{"info":{"name":"A Series"},"seasons":[],"episodes":{"1":[{"id":"101","episode_num":1}]}}""");

        using (client)
        {
            var series = await client.GetSeriesInfoAsync(12);

            Assert.Equal("A Series", series?.Details?.Name);
            Assert.Equal(101, Assert.Single(series!.AllEpisodes).EpisodeId);
            Assert.Contains("action=get_series_info", LastQuery(handler), StringComparison.Ordinal);
            Assert.Contains("series_id=12", LastQuery(handler), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_unknown_series_identifier_returns_null_rather_than_an_error()
    {
        // On an unknown identifier PHP serialises an empty array: the response is
        // "[]" rather than an object.
        var (client, _) = Create("[]");

        using (client)
        {
            Assert.Null(await client.GetSeriesInfoAsync(999));
        }
    }

    [Fact]
    public async Task An_unknown_movie_identifier_returns_null_rather_than_an_error()
    {
        var (client, _) = Create("[]");

        using (client)
        {
            Assert.Null(await client.GetVodInfoAsync(999));
        }
    }

    [Fact]
    public async Task A_movie_record_is_read()
    {
        var (client, handler) = Create(
            """{"info":{"name":"A Movie","duration_secs":8340},"movie_data":{"stream_id":555,"container_extension":"mkv"}}""");

        using (client)
        {
            var movie = await client.GetVodInfoAsync(555);

            Assert.Equal("A Movie", movie?.Details?.Name);
            Assert.Equal(555, movie?.MovieData?.StreamId);
            Assert.Contains("vod_id=555", LastQuery(handler), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_short_epg_passes_on_the_requested_limit()
    {
        var (client, handler) = Create("""{"epg_listings":[{"title":"Journal"}]}""");

        using (client)
        {
            await client.GetShortEpgAsync(42);
            Assert.Contains("action=get_short_epg", LastQuery(handler), StringComparison.Ordinal);
            Assert.Contains("stream_id=42", LastQuery(handler), StringComparison.Ordinal);
            Assert.DoesNotContain("limit=", LastQuery(handler), StringComparison.Ordinal);

            var listings = await client.GetShortEpgAsync(42, limit: 8);
            Assert.Contains("limit=8", LastQuery(handler), StringComparison.Ordinal);
            Assert.Equal("Journal", Assert.Single(listings).Title);
        }
    }

    [Fact]
    public async Task The_full_guide_of_a_channel_passes_its_stream_identifier()
    {
        // Calling get_simple_data_table without stream_id, a required parameter,
        // never returns anything.
        var (client, handler) = Create("""{"epg_listings":[]}""");

        using (client)
        {
            await client.GetEpgAsync(42);

            Assert.Contains("action=get_simple_data_table", LastQuery(handler), StringComparison.Ordinal);
            Assert.Contains("stream_id=42", LastQuery(handler), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_guide_is_read_even_when_the_panel_returns_a_bare_array()
    {
        var (client, _) = Create("""[{"title":"Journal","start_timestamp":"1767297600"}]""");

        using (client)
        {
            var listings = await client.GetEpgAsync(42);

            Assert.Equal("Journal", Assert.Single(listings).Title);
        }
    }

    [Fact]
    public async Task The_catchup_table_passes_its_bounds_in_the_expected_format()
    {
        var (client, handler) = Create("""{"epg_listings":[]}""");

        using (client)
        {
            await client.GetCatchupTableAsync(
                42,
                new DateTimeOffset(2026, 1, 15, 20, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 16, 20, 30, 0, TimeSpan.Zero));

            var query = Uri.UnescapeDataString(LastQuery(handler));

            Assert.Contains("action=get_catchup_table", query, StringComparison.Ordinal);
            Assert.Contains("start=2026-01-15:20-30", query, StringComparison.Ordinal);
            Assert.Contains("end=2026-01-16:20-30", query, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_xmltv_guide_is_opened_as_a_stream()
    {
        const string body = """<?xml version="1.0"?><tv><channel id="France2.fr" /></tv>""";
        using var handler = StubHttpMessageHandler.Responding(body, "text/xml");
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());
        using var client = new XtreamClient(Credentials, transport);

        var stream = await client.OpenXmltvAsync();

        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream);
            Assert.Equal(body, await reader.ReadToEndAsync());
        }

        Assert.Contains("xmltv.php", handler.Requests[^1].AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_playlist_is_opened_as_a_stream()
    {
        using var handler = StubHttpMessageHandler.Responding("#EXTM3U\n", "audio/x-mpegurl");
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());
        using var client = new XtreamClient(Credentials, transport);

        var stream = await client.OpenPlaylistAsync();

        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream);
            Assert.StartsWith("#EXTM3U", await reader.ReadToEndAsync(), StringComparison.Ordinal);
        }

        Assert.Contains("get.php", handler.Requests[^1].AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_call_asks_the_caller_for_credentials()
    {
        // The client owns them: absent from every signature, yet present in
        // every request.
        var (client, handler) = Create("[]");

        using (client)
        {
            await client.GetLiveStreamsAsync();

            Assert.Contains("username=demo", LastQuery(handler), StringComparison.Ordinal);
            Assert.Contains("password=secret", LastQuery(handler), StringComparison.Ordinal);
            Assert.Equal(Credentials, client.Credentials);
        }
    }

    [Fact]
    public async Task A_disposed_client_refuses_calls()
    {
        var (client, _) = Create("[]");
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetLiveCategoriesAsync());
    }

    [Fact]
    public async Task A_shared_transport_survives_the_disposal_of_the_client()
    {
        using var handler = StubHttpMessageHandler.Responding("""{"user_info":{"auth":1,"status":"Active"}}""");
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());

        using (new XtreamClient(Credentials, transport, ownsTransport: false))
        {
        }

        using var second = new XtreamClient(Credentials, transport, ownsTransport: false);

        Assert.True((await second.AuthenticateAsync()).IsUsable);
    }
}
