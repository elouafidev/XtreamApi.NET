using System.Globalization;
using System.Text.Json;
using XtreamApi.Http;
using XtreamApi.Models;
using XtreamApi.Serialization;

namespace XtreamApi;

/// <summary>
/// Client for an Xtream panel.
/// </summary>
public sealed class XtreamClient : IXtreamClient
{
    private const string CatchupTimeFormat = "yyyy-MM-dd:HH-mm";

    private readonly IXtreamTransport _transport;
    private readonly bool _ownsTransport;
    private bool _disposed;

    /// <summary>
    /// Creates a client that owns its transport.
    /// <para>
    /// Convenient for one-off use. In a long-lived application, prefer the
    /// constructor taking a shared transport: each transport creates its own
    /// <see cref="HttpClient"/>.
    /// </para>
    /// </summary>
    public XtreamClient(XtreamCredentials credentials, XtreamClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        Credentials = credentials;
        _transport = new XtreamHttpTransport(options);
        _ownsTransport = true;
    }

    /// <summary>
    /// Creates a client on top of a supplied transport.
    /// </summary>
    /// <param name="credentials">Access details for the panel.</param>
    /// <param name="transport">Transport to use for the calls.</param>
    /// <param name="ownsTransport">
    /// <c>true</c> so that disposing the client also disposes the transport.
    /// Leave <c>false</c> when the transport is shared between accounts.
    /// </param>
    public XtreamClient(XtreamCredentials credentials, IXtreamTransport transport, bool ownsTransport = false)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(transport);

        Credentials = credentials;
        _transport = transport;
        _ownsTransport = ownsTransport;
    }

    public XtreamCredentials Credentials { get; }

    public XtreamStreamUrlBuilder StreamUrls => XtreamStreamUrlBuilder.For(Credentials);

    public XtreamStreamUrlBuilder CreateStreamUrlBuilder(ServerInfo? serverInfo) =>
        XtreamStreamUrlBuilder.For(Credentials, serverInfo);

    public async Task<XtreamAccount> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        var account = await GetJsonAsync<XtreamAccount>(XtreamRequest.Account(), cancellationToken)
            .ConfigureAwait(false);

        return account ?? throw new XtreamProtocolException(
            "The panel returned no account information.");
    }

    public async Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var account = await GetAccountAsync(cancellationToken).ConfigureAwait(false);
        var user = account.User;

        if (user is null || !user.IsAuthenticated)
        {
            throw new XtreamAuthenticationException(
                "The panel rejected the credentials. Check the username and the password.");
        }

        // Panels routinely answer auth = 1 on a finished subscription: checking
        // authentication alone would suggest the connection succeeded, and then
        // every read would fail with no explanation.
        if (!user.IsUsable)
        {
            throw new XtreamAuthenticationException(DescribeUnusableAccount(user));
        }

        return account;
    }

    public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<XtreamCategory>(XtreamRequest.ForAction(XtreamActions.LiveCategories), cancellationToken);

    public Task<IReadOnlyList<XtreamCategory>> GetVodCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<XtreamCategory>(XtreamRequest.ForAction(XtreamActions.VodCategories), cancellationToken);

    public Task<IReadOnlyList<XtreamCategory>> GetSeriesCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<XtreamCategory>(XtreamRequest.ForAction(XtreamActions.SeriesCategories), cancellationToken);

    public Task<IReadOnlyList<LiveStream>> GetLiveStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default) =>
        GetListAsync<LiveStream>(Catalog(XtreamActions.LiveStreams, categoryId), cancellationToken);

    public Task<IReadOnlyList<VodStream>> GetVodStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default) =>
        GetListAsync<VodStream>(Catalog(XtreamActions.VodStreams, categoryId), cancellationToken);

    public Task<IReadOnlyList<SeriesSummary>> GetSeriesAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default) =>
        GetListAsync<SeriesSummary>(Catalog(XtreamActions.Series, categoryId), cancellationToken);

    public IAsyncEnumerable<LiveStream> StreamLiveStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default) =>
        StreamAsync<LiveStream>(Catalog(XtreamActions.LiveStreams, categoryId), cancellationToken);

    public IAsyncEnumerable<VodStream> StreamVodStreamsAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default) =>
        StreamAsync<VodStream>(Catalog(XtreamActions.VodStreams, categoryId), cancellationToken);

    public IAsyncEnumerable<SeriesSummary> StreamSeriesAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default) =>
        StreamAsync<SeriesSummary>(Catalog(XtreamActions.Series, categoryId), cancellationToken);

    public Task<VodInfo?> GetVodInfoAsync(int vodId, CancellationToken cancellationToken = default) =>
        GetSingleAsync<VodInfo>(
            XtreamRequest.ForAction(XtreamActions.VodInfo, ("vod_id", Number(vodId))),
            cancellationToken);

    public Task<SeriesInfo?> GetSeriesInfoAsync(int seriesId, CancellationToken cancellationToken = default) =>
        GetSingleAsync<SeriesInfo>(
            XtreamRequest.ForAction(XtreamActions.SeriesInfo, ("series_id", Number(seriesId))),
            cancellationToken);

    public Task<IReadOnlyList<EpgListing>> GetShortEpgAsync(
        int streamId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var request = limit is null
            ? XtreamRequest.ForAction(XtreamActions.ShortEpg, ("stream_id", Number(streamId)))
            : XtreamRequest.ForAction(
                XtreamActions.ShortEpg,
                ("stream_id", Number(streamId)),
                ("limit", Number(limit.Value)));

        return GetEpgAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<EpgListing>> GetEpgAsync(int streamId, CancellationToken cancellationToken = default) =>
        GetEpgAsync(
            XtreamRequest.ForAction(XtreamActions.SimpleDataTable, ("stream_id", Number(streamId))),
            cancellationToken);

    public Task<IReadOnlyList<EpgListing>> GetCatchupTableAsync(
        int streamId,
        DateTimeOffset? startInServerTime = null,
        DateTimeOffset? endInServerTime = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<(string Name, string Value)>(3)
        {
            ("stream_id", Number(streamId)),
        };

        if (startInServerTime is not null)
        {
            parameters.Add(("start", startInServerTime.Value.ToString(CatchupTimeFormat, CultureInfo.InvariantCulture)));
        }

        if (endInServerTime is not null)
        {
            parameters.Add(("end", endInServerTime.Value.ToString(CatchupTimeFormat, CultureInfo.InvariantCulture)));
        }

        return GetEpgAsync(
            XtreamRequest.ForAction(XtreamActions.CatchupTable, [.. parameters]),
            cancellationToken);
    }

    public Task<Stream> OpenXmltvAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _transport.OpenReadAsync(Credentials, XtreamRequest.Xmltv(), cancellationToken);
    }

    public Task<Stream> OpenPlaylistAsync(
        string type = "m3u_plus",
        string output = "ts",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _transport.OpenReadAsync(Credentials, XtreamRequest.Playlist(type, output), cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsTransport)
        {
            _transport.Dispose();
        }
    }

    private static XtreamRequest Catalog(string action, int? categoryId) =>
        categoryId is null
            ? XtreamRequest.ForAction(action)
            : XtreamRequest.ForAction(action, ("category_id", Number(categoryId.Value)));

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string DescribeUnusableAccount(UserInfo user) => user.Status switch
    {
        AccountStatus.Expired => user.ExpiresAt is { } expiry
            ? $"The subscription expired on {expiry.ToLocalTime():d}."
            : "The subscription has expired.",
        AccountStatus.Disabled => "The account was disabled by the provider.",
        AccountStatus.Banned => "The account was banned by the provider.",
        _ => user.ExpiresAt is { } date
            ? $"The subscription has not been valid since {date.ToLocalTime():d}."
            : "The account is not usable.",
    };

    private Task<T?> GetJsonAsync<T>(XtreamRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _transport.GetJsonAsync<T>(Credentials, request, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(XtreamRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var items = new List<T>();

        await foreach (var item in _transport
            .StreamJsonArrayAsync<T>(Credentials, request, cancellationToken)
            .ConfigureAwait(false))
        {
            items.Add(item);
        }

        return items;
    }

    private IAsyncEnumerable<T> StreamAsync<T>(XtreamRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _transport.StreamJsonArrayAsync<T>(Credentials, request, cancellationToken);
    }

    /// <summary>
    /// Reads a single detail record.
    /// <para>
    /// On an unknown identifier, panels return not an empty object but an empty
    /// array: an artefact of PHP's <c>json_encode</c>, which serialises an empty
    /// PHP array as <c>[]</c>. The response is therefore inspected before being
    /// converted.
    /// </para>
    /// </summary>
    private async Task<T?> GetSingleAsync<T>(XtreamRequest request, CancellationToken cancellationToken)
        where T : class
    {
        var element = await GetJsonAsync<JsonElement>(request, cancellationToken).ConfigureAwait(false);

        if (element.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return element.Deserialize<T>(XtreamJson.Default);
        }
        catch (JsonException exception)
        {
            throw new XtreamProtocolException(
                "The record returned by the panel does not have the expected shape.",
                Truncate(element),
                exception);
        }
    }

    /// <summary>
    /// Reads an EPG response.
    /// <para>
    /// Panels return sometimes <c>{"epg_listings": [...]}</c>, sometimes the
    /// bare array. Both shapes are accepted.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<EpgListing>> GetEpgAsync(
        XtreamRequest request,
        CancellationToken cancellationToken)
    {
        var element = await GetJsonAsync<JsonElement>(request, cancellationToken).ConfigureAwait(false);

        try
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => element.Deserialize<List<EpgListing>>(XtreamJson.Default) ?? [],
                JsonValueKind.Object => element.Deserialize<EpgResponse>(XtreamJson.Default)?.Listings ?? [],
                _ => [],
            };
        }
        catch (JsonException exception)
        {
            throw new XtreamProtocolException(
                "The guide returned by the panel does not have the expected shape.",
                Truncate(element),
                exception);
        }
    }

    private static string Truncate(JsonElement element)
    {
        var text = element.GetRawText();
        return text.Length <= 512 ? text : text[..512];
    }
}

/// <summary>
/// Action names of <c>player_api.php</c>.
/// </summary>
internal static class XtreamActions
{
    internal const string LiveCategories = "get_live_categories";
    internal const string VodCategories = "get_vod_categories";
    internal const string SeriesCategories = "get_series_categories";
    internal const string LiveStreams = "get_live_streams";
    internal const string VodStreams = "get_vod_streams";
    internal const string Series = "get_series";
    internal const string VodInfo = "get_vod_info";
    internal const string SeriesInfo = "get_series_info";
    internal const string ShortEpg = "get_short_epg";
    internal const string SimpleDataTable = "get_simple_data_table";
    internal const string CatchupTable = "get_catchup_table";
}
