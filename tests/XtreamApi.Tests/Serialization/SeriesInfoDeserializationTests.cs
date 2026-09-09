using System.Text.Json;
using XtreamApi.Models;
using XtreamApi.Serialization;

namespace XtreamApi.Tests.Serialization;

public class SeriesInfoDeserializationTests
{
    [Fact]
    public void Reads_episodes_indexed_by_season_number()
    {
        // "episodes" is an object keyed by season, not an array.
        const string json = """
        {
          "info": { "name": "A Series", "genre": "Drama", "episode_run_time": "45" },
          "seasons": [
            { "id": 1, "season_number": 1, "name": "Saison 1", "episode_count": 2 }
          ],
          "episodes": {
            "1": [
              { "id": "101", "episode_num": 1, "title": "Pilote", "container_extension": "mkv", "season": 1,
                "info": { "duration_secs": 2700, "duration": "00:45:00", "rating": "8.1" } },
              { "id": "102", "episode_num": 2, "title": "Suite", "container_extension": "mkv", "season": 1 }
            ],
            "2": [
              { "id": "201", "episode_num": 1, "title": "Retour", "container_extension": "mp4", "season": 2 }
            ]
          }
        }
        """;

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        Assert.NotNull(series);
        Assert.Equal("A Series", series.Details?.Name);
        Assert.Equal(45, series.Details?.EpisodeRunTime);
        Assert.Equal([1, 2], series.SeasonNumbers);
        Assert.Equal(3, series.AllEpisodes.Count);

        var pilot = series.GetSeason(1)[0];
        Assert.Equal(101, pilot.EpisodeId);
        Assert.Equal(101, pilot.Id);
        Assert.Equal(StreamKind.Series, pilot.Kind);
        Assert.Equal("Pilote", pilot.DisplayTitle);
        Assert.Equal(TimeSpan.FromMinutes(45), pilot.Details?.Duration);
        Assert.Equal(8.1d, pilot.Details?.Rating);
    }

    [Fact]
    public void Episodes_are_flattened_in_season_then_episode_order()
    {
        const string json = """
        {
          "episodes": {
            "2": [ { "id": "203", "episode_num": 3 }, { "id": "201", "episode_num": 1 } ],
            "1": [ { "id": "102", "episode_num": 2 }, { "id": "101", "episode_num": 1 } ]
          }
        }
        """;

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        Assert.Equal([101, 102, 201, 203], series!.AllEpisodes.Select(episode => episode.EpisodeId));
    }

    [Fact]
    public void A_series_with_no_episode_returned_as_an_empty_array_is_accepted()
    {
        // PHP's json_encode returns [] rather than {} for an empty array: the
        // shape of the field changes with whether the series has episodes.
        const string json = """{"info":{"name":"Vide"},"seasons":[],"episodes":[]}""";

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        Assert.NotNull(series);
        Assert.Empty(series.Episodes);
        Assert.Empty(series.AllEpisodes);
        Assert.Empty(series.SeasonNumbers);
        Assert.Empty(series.GetSeason(1));
    }

    [Fact]
    public void A_non_numeric_season_key_is_skipped_without_losing_the_rest()
    {
        const string json = """
        {"episodes":{"specials":[{"id":"900","episode_num":1}],"1":[{"id":"101","episode_num":1}]}}
        """;

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        Assert.Equal([1], series!.SeasonNumbers);
        Assert.Equal(101, Assert.Single(series.AllEpisodes).EpisodeId);
    }

    [Fact]
    public void Episodes_returned_as_an_array_of_arrays_are_accepted()
    {
        const string json = """
        {"episodes":[[{"id":"101","episode_num":1,"season":1}],[{"id":"201","episode_num":1,"season":2}]]}
        """;

        var series = JsonSerializer.Deserialize<SeriesInfo>(json, XtreamJson.Default);

        Assert.Equal([1, 2], series!.SeasonNumbers);
        Assert.Equal(2, series.AllEpisodes.Count);
    }

    [Fact]
    public void Reads_the_detailed_record_of_a_movie()
    {
        const string json = """
        {
          "info": {
            "tmdb_id": "550",
            "name": "A Movie",
            "o_name": "A Movie",
            "releasedate": "1999-10-15",
            "duration_secs": 8340,
            "duration": "02:19:00",
            "rating": "8.8",
            "backdrop_path": ["http://panel.example.com/bd1.jpg", "http://panel.example.com/bd2.jpg"],
            "plot": "Un synopsis.",
            "video": { "codec_name": "h264", "width": 1920 },
            "audio": { "codec_name": "aac", "channels": 6 }
          },
          "movie_data": {
            "stream_id": 555,
            "name": "A Movie",
            "title": "A Movie",
            "year": "1999",
            "container_extension": "mkv",
            "category_id": "3",
            "added": "1700000000"
          }
        }
        """;

        var vod = JsonSerializer.Deserialize<VodInfo>(json, XtreamJson.Default);

        Assert.NotNull(vod);
        Assert.Equal("A Movie", vod.Details?.OriginalName);
        Assert.Equal(TimeSpan.FromSeconds(8340), vod.Details?.Duration);
        Assert.Equal(8.8d, vod.Details?.Rating);
        Assert.Equal(2, vod.Details?.BackdropPaths.Count);
        Assert.Equal("Un synopsis.", vod.Details?.Synopsis);
        Assert.Equal("h264", vod.Details?.Video?.GetProperty("codec_name").GetString());
        Assert.Equal(555, vod.MovieData?.StreamId);
        Assert.Equal("mkv", vod.MovieData?.ContainerExtension);
    }

    [Fact]
    public void The_synopsis_falls_back_to_description_when_plot_is_missing()
    {
        const string json = """{"info":{"description":"Texte de repli"}}""";

        var vod = JsonSerializer.Deserialize<VodInfo>(json, XtreamJson.Default);

        Assert.Equal("Texte de repli", vod?.Details?.Synopsis);
    }
}
