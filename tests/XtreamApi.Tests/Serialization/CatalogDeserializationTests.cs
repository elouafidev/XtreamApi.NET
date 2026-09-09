using System.Text.Json;
using XtreamApi.Models;
using XtreamApi.Serialization;

namespace XtreamApi.Tests.Serialization;

public class CatalogDeserializationTests
{
    [Fact]
    public void Reads_a_live_channel()
    {
        const string json = """
        [{
          "num": 1,
          "name": "France 2 FHD",
          "stream_type": "live",
          "stream_id": 12345,
          "stream_icon": "http://panel.example.com/logo.png",
          "epg_channel_id": "France2.fr",
          "added": "1700000000",
          "is_adult": "0",
          "category_id": "7",
          "category_ids": [7, 19],
          "custom_sid": "",
          "tv_archive": 1,
          "direct_source": "",
          "tv_archive_duration": 7
        }]
        """;

        var streams = JsonSerializer.Deserialize<List<LiveStream>>(json, XtreamJson.Default);

        var channel = Assert.Single(streams!);
        Assert.Equal(12345, channel.StreamId);
        Assert.Equal(12345, channel.Id);
        Assert.Equal(StreamKind.Live, channel.Kind);
        Assert.Equal(7, channel.CategoryId);
        Assert.Equal([7, 19], channel.AllCategoryIds);
        Assert.False(channel.IsAdult);
        Assert.True(channel.HasCatchup);
        Assert.True(channel.HasEpg);
    }

    [Fact]
    public void A_channel_without_category_ids_falls_back_to_the_single_category()
    {
        const string json = """[{"stream_id":1,"category_id":"42"}]""";

        var streams = JsonSerializer.Deserialize<List<LiveStream>>(json, XtreamJson.Default);

        Assert.Equal([42], Assert.Single(streams!).AllCategoryIds);
    }

    [Fact]
    public void A_channel_with_no_category_at_all_does_not_throw()
    {
        const string json = """[{"stream_id":1,"category_id":null,"category_ids":null}]""";

        var streams = JsonSerializer.Deserialize<List<LiveStream>>(json, XtreamJson.Default);

        var channel = Assert.Single(streams!);
        Assert.Null(channel.CategoryId);
        Assert.Empty(channel.CategoryIds);
        Assert.Empty(channel.AllCategoryIds);
    }

    [Fact]
    public void An_archive_declared_without_depth_is_not_catchup()
    {
        const string json = """[{"stream_id":1,"tv_archive":1,"tv_archive_duration":0}]""";

        var streams = JsonSerializer.Deserialize<List<LiveStream>>(json, XtreamJson.Default);

        var channel = Assert.Single(streams!);
        Assert.True(channel.TvArchive);
        Assert.False(channel.HasCatchup);
    }

    [Fact]
    public void Reads_a_movie_whose_rating_is_an_empty_string()
    {
        // Very common: the panel has no rating and returns "" instead of null.
        const string json = """
        [{
          "num": 1,
          "name": "A Movie",
          "stream_type": "movie",
          "stream_id": 555,
          "rating": "",
          "rating_5based": 0,
          "container_extension": "mkv",
          "category_id": 3,
          "added": "1700000000"
        }]
        """;

        var movies = JsonSerializer.Deserialize<List<VodStream>>(json, XtreamJson.Default);

        var movie = Assert.Single(movies!);
        Assert.Null(movie.Rating);
        Assert.Equal(0d, movie.Rating5Based);
        Assert.Equal("mkv", movie.ContainerExtension);
        Assert.Equal(StreamKind.Movie, movie.Kind);
        Assert.Equal("A Movie", movie.DisplayTitle);
    }

    [Fact]
    public void A_textual_floating_point_rating_is_read()
    {
        const string json = """[{"stream_id":1,"rating":"7.8","rating_5based":"3.9"}]""";

        var movies = JsonSerializer.Deserialize<List<VodStream>>(json, XtreamJson.Default);

        var movie = Assert.Single(movies!);
        Assert.Equal(7.8d, movie.Rating);
        Assert.Equal(3.9d, movie.Rating5Based);
    }

    [Fact]
    public void A_backdrop_returned_as_a_single_string_becomes_a_list()
    {
        const string json = """
        [{"series_id":9,"name":"A Series","backdrop_path":"http://panel.example.com/bd.jpg"}]
        """;

        var series = JsonSerializer.Deserialize<List<SeriesSummary>>(json, XtreamJson.Default);

        Assert.Equal(["http://panel.example.com/bd.jpg"], Assert.Single(series!).BackdropPaths);
    }

    [Fact]
    public void A_missing_backdrop_yields_an_empty_list_not_null()
    {
        const string json = """[{"series_id":9,"backdrop_path":null}]""";

        var series = JsonSerializer.Deserialize<List<SeriesSummary>>(json, XtreamJson.Default);

        Assert.Empty(Assert.Single(series!).BackdropPaths);
    }

    [Theory]
    [InlineData("releaseDate")]
    [InlineData("release_date")]
    public void The_release_date_is_read_whichever_field_name_is_used(string fieldName)
    {
        var json = $$"""[{"series_id":9,"{{fieldName}}":"2024-03-15"}]""";

        var series = JsonSerializer.Deserialize<List<SeriesSummary>>(json, XtreamJson.Default);

        Assert.Equal(
            new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero),
            Assert.Single(series!).ReleaseDate);
    }

    [Fact]
    public void An_empty_mysql_date_is_treated_as_missing()
    {
        const string json = """[{"series_id":9,"releaseDate":"0000-00-00"}]""";

        var series = JsonSerializer.Deserialize<List<SeriesSummary>>(json, XtreamJson.Default);

        Assert.Null(Assert.Single(series!).ReleaseDate);
    }

    [Fact]
    public void Reads_a_category_whose_identifier_is_a_number_or_a_string()
    {
        const string json = """
        [{"category_id":"1","category_name":"Sport","parent_id":0},
         {"category_id":2,"category_name":"Cinema","parent_id":"1"}]
        """;

        var categories = JsonSerializer.Deserialize<List<XtreamCategory>>(json, XtreamJson.Default);

        Assert.Equal(2, categories!.Count);
        Assert.Equal(1, categories[0].Id);
        Assert.Equal(0, categories[0].ParentId);
        Assert.Equal(2, categories[1].Id);
        Assert.Equal(1, categories[1].ParentId);
    }

    [Fact]
    public void A_numeric_field_returned_as_an_object_does_not_break_the_read()
    {
        // Seen on cobbled-together panels: an object where an integer is
        // expected. The item must stay usable rather than fail the response.
        const string json = """[{"stream_id":77,"category_id":{"unexpected":true},"name":"Channel"}]""";

        var streams = JsonSerializer.Deserialize<List<LiveStream>>(json, XtreamJson.Default);

        var channel = Assert.Single(streams!);
        Assert.Equal(77, channel.StreamId);
        Assert.Equal("Channel", channel.Name);
        Assert.Null(channel.CategoryId);
    }
}
