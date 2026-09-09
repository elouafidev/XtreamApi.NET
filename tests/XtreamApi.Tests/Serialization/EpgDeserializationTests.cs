using System.Text;
using System.Text.Json;
using XtreamApi.Models;
using XtreamApi.Serialization;

namespace XtreamApi.Tests.Serialization;

public class EpgDeserializationTests
{
    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Reads_an_epg_response_and_decodes_the_base64_fields()
    {
        var json = $$"""
        {
          "epg_listings": [
            {
              "id": "981231",
              "epg_id": "17",
              "title": "{{Base64("The Eight O'Clock News")}}",
              "lang": "fr",
              "start": "2026-01-01 20:00:00",
              "end": "2026-01-01 20:40:00",
              "description": "{{Base64("All of today's news.")}}",
              "channel_id": "France2.fr",
              "start_timestamp": "1767297600",
              "stop_timestamp": "1767300000",
              "now_playing": 1,
              "has_archive": 0
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        var listing = Assert.Single(response!.Listings);
        Assert.Equal("The Eight O'Clock News", listing.Title);
        Assert.Equal("All of today's news.", listing.Description);
        Assert.Equal("France2.fr", listing.ChannelId);
        Assert.True(listing.NowPlaying);
        Assert.False(listing.HasArchive);
        Assert.Equal(TimeSpan.FromMinutes(40), listing.Duration);
    }

    [Fact]
    public void The_epoch_timestamp_wins_over_the_text_field()
    {
        // The text field is in the server's time zone, the epoch is in UTC.
        // Trusting the text shifts the whole schedule for a server outside UTC.
        const string json = """
        {"epg_listings":[{"start":"2026-01-01 21:00:00","start_timestamp":"1767297600"}]}
        """;

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        var listing = Assert.Single(response!.Listings);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1767297600), listing.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 21, 0, 0, TimeSpan.Zero), listing.StartLocal);
    }

    [Fact]
    public void Without_an_epoch_timestamp_the_text_field_is_the_fallback()
    {
        const string json = """{"epg_listings":[{"start":"2026-01-01 20:00:00"}]}""";

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        Assert.Equal(
            new DateTimeOffset(2026, 1, 1, 20, 0, 0, TimeSpan.Zero),
            Assert.Single(response!.Listings).StartsAt);
    }

    [Fact]
    public void A_plain_title_is_left_untouched()
    {
        // Not every panel encodes. Decoding blindly would yield binary.
        const string json = """{"epg_listings":[{"title":"Journal","description":"In the clear"}]}""";

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        var listing = Assert.Single(response!.Listings);
        Assert.Equal("Journal", listing.Title);
        Assert.Equal("In the clear", listing.Description);
    }

    [Theory]
    // Length a multiple of four and a valid alphabet: these plain titles pass
    // the Base64 shape test and must still come back untouched.
    [InlineData("Info")]
    [InlineData("Sport")]
    [InlineData("NEWS")]
    [InlineData("Cinema")]
    public void A_plain_title_that_looks_like_base64_comes_back_intact(string title)
    {
        var json = $$"""{"epg_listings":[{"title":"{{title}}"}]}""";

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        Assert.Equal(title, Assert.Single(response!.Listings).Title);
    }

    [Fact]
    public void An_accent_encoded_in_base64_is_restored()
    {
        var encoded = Base64("Meteo a Notre-Dame, ete 2026");
        var json = $$"""{"epg_listings":[{"title":"{{encoded}}"}]}""";

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        Assert.Equal("Meteo a Notre-Dame, ete 2026", Assert.Single(response!.Listings).Title);
    }

    [Fact]
    public void An_empty_epg_response_yields_an_empty_list()
    {
        const string json = """{"epg_listings":[]}""";

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        Assert.Empty(response!.Listings);
    }

    [Fact]
    public void A_missing_end_bound_leaves_the_duration_undetermined()
    {
        const string json = """{"epg_listings":[{"start_timestamp":"1767297600"}]}""";

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        var listing = Assert.Single(response!.Listings);
        Assert.Null(listing.EndsAt);
        Assert.Null(listing.Duration);
        Assert.False(listing.IsOnAir);
    }
}
