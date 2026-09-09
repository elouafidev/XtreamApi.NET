using System.Text.Json;
using XtreamApi.Models;
using XtreamApi.Serialization;

namespace XtreamApi.Tests;

public class XtreamStreamUrlBuilderTests
{
    private static readonly XtreamCredentials Credentials =
        XtreamCredentials.Create("http://panel.example.com:8080", "demo", "secret");

    private static readonly XtreamStreamUrlBuilder Builder = XtreamStreamUrlBuilder.For(Credentials);

    private static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, XtreamJson.Default)!;

    [Fact]
    public void Builds_a_live_channel_url()
    {
        Assert.Equal(
            "http://panel.example.com:8080/live/demo/secret/12345.ts",
            Builder.BuildLive(12345).ToString());
    }

    [Fact]
    public void The_live_container_can_be_chosen()
    {
        Assert.Equal(
            "http://panel.example.com:8080/live/demo/secret/12345.m3u8",
            Builder.BuildLive(12345, "m3u8").ToString());
    }

    [Fact]
    public void Builds_a_movie_url_with_its_container()
    {
        var movie = Read<VodStream>("""{"stream_id":555,"container_extension":"mkv"}""");

        Assert.Equal(
            "http://panel.example.com:8080/movie/demo/secret/555.mkv",
            Builder.BuildMovie(movie).ToString());
    }

    [Fact]
    public void A_movie_with_no_declared_container_falls_back_to_mp4()
    {
        var movie = Read<VodStream>("""{"stream_id":555}""");

        Assert.EndsWith("/movie/demo/secret/555.mp4", Builder.BuildMovie(movie).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_leading_dot_in_the_container_is_not_doubled()
    {
        var movie = Read<VodStream>("""{"stream_id":555,"container_extension":".mkv"}""");

        Assert.EndsWith("/555.mkv", Builder.BuildMovie(movie).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_episode_url_uses_the_episode_identifier()
    {
        // A classic trap: using series_id here leads nowhere.
        var episode = Read<Episode>("""{"id":"101","episode_num":1,"container_extension":"mkv","season":1}""");

        Assert.Equal(
            "http://panel.example.com:8080/series/demo/secret/101.mkv",
            Builder.BuildEpisode(episode).ToString());
    }

    [Fact]
    public void An_address_imposed_by_the_provider_is_returned_as_is()
    {
        var channel = Read<LiveStream>(
            """{"stream_id":1,"direct_source":"http://autre.example.com/flux/abc.ts"}""");

        Assert.Equal("http://autre.example.com/flux/abc.ts", Builder.BuildLive(channel).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    // A non-http scheme must not be handed to a video player.
    [InlineData("file:///C:/secret.txt")]
    public void An_unusable_imposed_address_is_ignored(string directSource)
    {
        var channel = Read<LiveStream>(
            $$"""{"stream_id":1,"direct_source":{{JsonSerializer.Serialize(directSource)}}}""");

        Assert.Equal(
            "http://panel.example.com:8080/live/demo/secret/1.ts",
            Builder.BuildLive(channel).ToString());
    }

    [Fact]
    public void A_password_with_special_characters_is_escaped_in_the_path()
    {
        // An unescaped "/" would point the URL at a completely different path.
        var credentials = XtreamCredentials.Create("http://panel.example.com", "de/mo", "a/b?c#d");
        var builder = XtreamStreamUrlBuilder.For(credentials);

        Assert.Equal(
            "http://panel.example.com/live/de%2Fmo/a%2Fb%3Fc%23d/7.ts",
            builder.BuildLive(7).ToString());
    }

    [Fact]
    public void The_streaming_host_declared_by_the_panel_wins_over_the_api_host()
    {
        // Several providers serve the API and the streams from two hosts.
        var serverInfo = Read<ServerInfo>(
            """{"url":"stream.example.com","port":"2095","server_protocol":"http"}""");

        var builder = XtreamStreamUrlBuilder.For(Credentials, serverInfo);

        Assert.Equal("http://stream.example.com:2095/", builder.BaseAddress.ToString());
        Assert.Equal(
            "http://stream.example.com:2095/live/demo/secret/1.ts",
            builder.BuildLive(1).ToString());
    }

    [Fact]
    public void Without_server_information_the_api_address_is_kept()
    {
        var builder = XtreamStreamUrlBuilder.For(Credentials, serverInfo: null);

        Assert.Equal("http://panel.example.com:8080/", builder.BaseAddress.ToString());
    }

    [Fact]
    public void Builds_a_catchup_url()
    {
        var start = new DateTimeOffset(2026, 1, 15, 20, 30, 0, TimeSpan.Zero);

        Assert.Equal(
            "http://panel.example.com:8080/timeshift/demo/secret/90/2026-01-15%3A20-30/12345.ts",
            Builder.BuildCatchup(12345, start, TimeSpan.FromMinutes(90)).ToString());
    }

    [Fact]
    public void The_catchup_duration_is_rounded_to_the_minute()
    {
        var start = new DateTimeOffset(2026, 1, 15, 20, 30, 0, TimeSpan.Zero);

        var uri = Builder.BuildCatchup(12345, start, TimeSpan.FromSeconds(3630));

        Assert.Contains("/61/", uri.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(20)]
    public void A_catchup_duration_below_one_minute_is_refused(int seconds)
    {
        var start = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Builder.BuildCatchup(1, start, TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void BuildFor_picks_the_segment_from_the_item_kind()
    {
        var channel = Read<LiveStream>("""{"stream_id":1}""");
        var movie = Read<VodStream>("""{"stream_id":2,"container_extension":"mp4"}""");
        var episode = Read<Episode>("""{"id":"3","container_extension":"mkv"}""");

        Assert.Contains("/live/", Builder.BuildFor(channel).ToString(), StringComparison.Ordinal);
        Assert.Contains("/movie/", Builder.BuildFor(movie).ToString(), StringComparison.Ordinal);
        Assert.Contains("/series/", Builder.BuildFor(episode).ToString(), StringComparison.Ordinal);
    }
}
