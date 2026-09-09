using System.Net;
using System.Text;
using XtreamApi.Http;
using XtreamApi.Models;

namespace XtreamApi.Tests.Http;

public class ProgressReportingTests
{
    private static readonly XtreamCredentials Credentials =
        XtreamCredentials.Create("http://panel.example.com:8080", "demo", "secret");

    private static XtreamClientOptions FastOptions() => new()
    {
        RetryCount = 0,
        ResponseHeadersTimeout = TimeSpan.FromSeconds(5),
    };

    /// <summary>A catalogue large enough to cross the reporting threshold.</summary>
    private static string BuildCatalog(int count) =>
        "[" + string.Join(',', Enumerable.Range(1, count)
            .Select(index => $$"""{"stream_id":{{index}},"name":"Channel number {{index}} in high definition"}"""))
        + "]";

    [Fact]
    public async Task Reports_progress_with_the_announced_size()
    {
        var body = BuildCatalog(20_000);
        var expectedBytes = Encoding.UTF8.GetByteCount(body);

        using var handler = StubHttpMessageHandler.Responding(body);
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());

        var reports = new List<XtreamProgress>();
        transport.Progress += (_, progress) => reports.Add(progress);

        await foreach (var _ in transport.StreamJsonArrayAsync<LiveStream>(
            Credentials,
            XtreamRequest.ForAction("get_live_streams")))
        {
        }

        Assert.NotEmpty(reports);
        Assert.All(reports, report => Assert.Equal(expectedBytes, report.TotalBytes));
        Assert.All(reports, report => Assert.True(report.IsMeasurable));

        // The final report must cover the whole response.
        Assert.Equal(expectedBytes, reports[^1].BytesReceived);
        Assert.Equal(100d, reports[^1].Percentage);
    }

    [Fact]
    public async Task Progress_advances_and_never_goes_backwards()
    {
        using var handler = StubHttpMessageHandler.Responding(BuildCatalog(20_000));
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());

        var received = new List<long>();
        transport.Progress += (_, progress) => received.Add(progress.BytesReceived);

        await foreach (var _ in transport.StreamJsonArrayAsync<LiveStream>(
            Credentials,
            XtreamRequest.ForAction("get_live_streams")))
        {
        }

        // A catalogue this size produces several reports: that is what moves the
        // bar along instead of making it jump at the end.
        Assert.True(received.Count > 1, $"un seul rapport recu ({received.Count})");
        Assert.Equal(received.Order(), received);
    }

    [Fact]
    public async Task A_response_without_an_announced_size_is_still_measured_in_bytes()
    {
        // A server replying in chunks, or one whose response .NET decompressed,
        // announces no length.
        using var handler = StubHttpMessageHandler.RespondingWithoutLength(BuildCatalog(5_000));
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());

        var reports = new List<XtreamProgress>();
        transport.Progress += (_, progress) => reports.Add(progress);

        await foreach (var _ in transport.StreamJsonArrayAsync<LiveStream>(
            Credentials,
            XtreamRequest.ForAction("get_live_streams")))
        {
        }

        Assert.NotEmpty(reports);
        Assert.All(reports, report => Assert.Null(report.TotalBytes));
        Assert.All(reports, report => Assert.False(report.IsMeasurable));
        Assert.All(reports, report => Assert.Null(report.Percentage));
        Assert.True(reports[^1].BytesReceived > 0);
    }

    [Fact]
    public async Task A_single_call_reports_its_progress_too()
    {
        const string body = """{"user_info":{"auth":1,"status":"Active"}}""";

        using var handler = StubHttpMessageHandler.Responding(body);
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());

        var reports = new List<XtreamProgress>();
        transport.Progress += (_, progress) => reports.Add(progress);

        await transport.GetJsonAsync<XtreamAccount>(Credentials, XtreamRequest.Account());

        var last = Assert.Single(reports);
        Assert.Equal(Encoding.UTF8.GetByteCount(body), last.BytesReceived);
        Assert.Equal(100d, last.Percentage);
    }

    [Fact]
    public async Task A_subscriber_that_throws_does_not_interrupt_the_read()
    {
        // Progress is a convenience: it must not be able to fail the catalogue
        // load.
        using var handler = StubHttpMessageHandler.Responding(BuildCatalog(2_000));
        using var transport = new XtreamHttpTransport(handler.CreateClient(), FastOptions());

        transport.Progress += (_, _) => throw new InvalidOperationException("faulty subscriber");

        var count = 0;
        await foreach (var _ in transport.StreamJsonArrayAsync<LiveStream>(
            Credentials,
            XtreamRequest.ForAction("get_live_streams")))
        {
            count++;
        }

        Assert.Equal(2_000, count);
    }

    [Theory]
    [InlineData(0, 100, 0d)]
    [InlineData(25, 100, 25d)]
    [InlineData(100, 100, 100d)]
    // A wrong announced size must not produce a nonsensical percentage.
    [InlineData(150, 100, 100d)]
    public void The_percentage_is_clamped(long received, long total, double expected)
    {
        Assert.Equal(expected, new XtreamProgress(received, total).Percentage);
    }

    [Fact]
    public void Without_a_total_size_the_percentage_is_undetermined()
    {
        var progress = new XtreamProgress(4096, null);

        Assert.Null(progress.Percentage);
        Assert.Null(progress.Fraction);
        Assert.False(progress.IsMeasurable);
        Assert.Equal(4096, progress.BytesReceived);
    }

    [Fact]
    public void A_zero_total_size_causes_no_division_by_zero()
    {
        Assert.Null(new XtreamProgress(0, 0).Percentage);
    }
}
