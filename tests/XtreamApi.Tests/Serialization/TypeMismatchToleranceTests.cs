using System.Text.Json;
using XtreamApi.Models;
using XtreamApi.Serialization;

namespace XtreamApi.Tests.Serialization;

/// <summary>
/// Cases where the panel sends a type other than the expected one.
/// <para>
/// These payloads come from real panels. Each one used to fail the entire
/// response over a field the caller never uses.
/// </para>
/// </summary>
public class TypeMismatchToleranceTests
{
    [Fact]
    public void A_numeric_tmdb_id_is_read_as_text()
    {
        // A real panel: tmdb_id comes out as a number when the column is filled,
        // as a string when it is not.
        const string json = """
        {"episodes":{"1":[{"id":"101","episode_num":1,"info":{"tmdb_id":1399,"duration_secs":2700}}]}}
        """;

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        var episode = Assert.Single(series!.AllEpisodes);
        Assert.Equal("1399", episode.Details?.TmdbId);
        Assert.Equal(TimeSpan.FromMinutes(45), episode.Details?.Duration);
    }

    [Fact]
    public void A_numeric_year_is_read_as_text()
    {
        const string json = """[{"stream_id":1,"name":"A Movie","year":1999,"tmdb_id":550}]""";

        var movies = JsonSerializer.Deserialize<List<VodStream>>(json, XtreamJson.Default);

        var movie = Assert.Single(movies!);
        Assert.Equal("1999", movie.Year);
        Assert.Equal("550", movie.TmdbId);
    }

    [Fact]
    public void An_identifier_too_large_for_an_integer_keeps_its_exact_value()
    {
        // Returning the raw text rather than going through a numeric type avoids
        // losing precision on long identifiers.
        const string json = """{"info":{"tmdb_id":123456789012345678901234567890}}""";

        var vod = JsonSerializer.Deserialize<VodInfo>(json, XtreamJson.Default);

        Assert.Equal("123456789012345678901234567890", vod?.Details?.TmdbId);
    }

    [Fact]
    public void A_text_field_that_is_null_stays_null()
    {
        const string json = """[{"stream_id":1,"year":null,"tmdb_id":null}]""";

        var movies = JsonSerializer.Deserialize<List<VodStream>>(json, XtreamJson.Default);

        var movie = Assert.Single(movies!);
        Assert.Null(movie.Year);
        Assert.Null(movie.TmdbId);
    }

    [Fact]
    public void A_text_field_returned_as_an_object_does_not_break_the_read()
    {
        const string json = """[{"stream_id":7,"name":"A Movie","tmdb_id":{"id":550}}]""";

        var movies = JsonSerializer.Deserialize<List<VodStream>>(json, XtreamJson.Default);

        var movie = Assert.Single(movies!);
        Assert.Equal(7, movie.StreamId);
        Assert.Equal("A Movie", movie.Name);
        Assert.Null(movie.TmdbId);
    }

    [Fact]
    public void Seasons_returned_as_an_indexed_object_are_read()
    {
        // PHP returns an object as soon as one entry has been removed.
        const string json = """
        {"seasons":{"0":{"season_number":1,"name":"Saison 1"},"2":{"season_number":3,"name":"Saison 3"}}}
        """;

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        Assert.Equal(2, series!.Seasons.Count);
        Assert.Equal([1, 3], series.Seasons.Select(season => season.Number));
    }

    [Fact]
    public void Null_seasons_yield_an_empty_list()
    {
        const string json = """{"seasons":null,"episodes":{}}""";

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        Assert.Empty(series!.Seasons);
    }

    [Fact]
    public void A_guide_returned_as_an_indexed_object_is_read()
    {
        const string json = """
        {"epg_listings":{"0":{"id":1,"title":"Journal"},"1":{"id":2,"title":"Meteo"}}}
        """;

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        Assert.Equal(2, response!.Listings.Count);
        Assert.Equal(["Journal", "Meteo"], response.Listings.Select(listing => listing.Title));
    }

    [Fact]
    public void A_null_guide_yields_an_empty_list()
    {
        const string json = """{"epg_listings":null}""";

        var response = JsonSerializer.Deserialize<EpgResponse>(json, XtreamJson.Default);

        Assert.Empty(response!.Listings);
    }

    [Fact]
    public void A_numeric_custom_sid_does_not_break_the_read()
    {
        const string json = """[{"stream_id":1,"name":"Channel","custom_sid":12345,"direct_source":""}]""";

        var streams = JsonSerializer.Deserialize<List<LiveStream>>(json, XtreamJson.Default);

        Assert.Equal("12345", Assert.Single(streams!).CustomSid);
    }

    [Fact]
    public void A_boolean_in_a_text_field_is_read()
    {
        const string json = """[{"stream_id":1,"tmdb_id":false}]""";

        var movies = JsonSerializer.Deserialize<List<VodStream>>(json, XtreamJson.Default);

        Assert.Equal("false", Assert.Single(movies!).TmdbId);
    }
}
