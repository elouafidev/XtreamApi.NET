# Xtream API SDK for .NET

A .NET client for the **Xtream Codes** API — the panel API behind most IPTV
providers. It covers authentication, categories, live channels, movies, series,
the electronic programme guide, and playback URL construction.

Targets **.NET 8.0** and **.NET 10.0**. No third-party dependencies.

```bash
dotnet add package XtreamApi
```

## Intended use and disclaimer

**This library is a protocol client, nothing more.** It ships no content, no
stream, no channel list, no provider address and no credentials. On its own it
gives access to nothing: it can only talk to a server whose address and
credentials *you* supply.

It is published for use with Xtream services you are lawfully entitled to
access — a panel you operate, a provider you hold a valid subscription with, or
a server you have been authorised to query.

Using it to reach services that distribute content without the rights holders'
permission falls outside its intended purpose. Anyone who does so acts on their
own initiative and bears sole responsibility for it, including for any breach of
copyright law, of local regulation, or of a provider's terms of service.

The author publishes this code as-is under the MIT License, which disclaims all
warranty and all liability, and accepts no responsibility whatsoever for how
third parties choose to use it. Nothing here constitutes legal advice: if you
are unsure whether your use is lawful where you live, seek qualified counsel.

## Why another Xtream client

Xtream panels are wildly inconsistent. The same field comes back as a number on
one provider and a string on the next, as an array when the list is full and as
an object once an entry has been deleted, with dates that are not ISO 8601 and
programme titles encoded in Base64. A client that reads them strictly fails on
the first oddity — and takes the whole response down with it, for a field the
caller never asked about.

This SDK is built around that reality. **Tolerance is the feature**, and it is
covered by 171 tests written from payloads that real panels actually return.

## Quick start

```csharp
using XtreamApi;

var credentials = XtreamCredentials.Create("http://panel.example.com:8080", "user", "pass");
// ...or from a playlist URL:
// var credentials = XtreamCredentials.FromPlaylistUrl(url);

using var client = new XtreamClient(credentials);

// Throws if the account is rejected, expired, disabled or banned.
var account = await client.AuthenticateAsync(cancellationToken);

Console.WriteLine($"Expires: {account.User?.ExpiresAt:d}");
Console.WriteLine($"Connections: {account.User?.ActiveConnections}/{account.User?.MaxConnections}");

// Playback URLs must be built from the streaming host the panel declares,
// which is not always the host that serves the API.
var urls = client.CreateStreamUrlBuilder(account.Server);

foreach (var category in await client.GetLiveCategoriesAsync(cancellationToken))
{
    Console.WriteLine(category.Name);
}

// Stream large catalogues instead of loading them whole: a single
// get_live_streams response routinely exceeds several tens of megabytes.
await foreach (var channel in client.StreamLiveStreamsAsync(cancellationToken: cancellationToken))
{
    Console.WriteLine($"{channel.Name} -> {urls.BuildLive(channel)}");
}
```

## What it covers

| Xtream action | Method |
| --- | --- |
| authentication | `AuthenticateAsync` / `GetAccountAsync` |
| `get_live_categories` | `GetLiveCategoriesAsync` |
| `get_vod_categories` | `GetVodCategoriesAsync` |
| `get_series_categories` | `GetSeriesCategoriesAsync` |
| `get_live_streams` | `GetLiveStreamsAsync` / `StreamLiveStreamsAsync` |
| `get_vod_streams` | `GetVodStreamsAsync` / `StreamVodStreamsAsync` |
| `get_series` | `GetSeriesAsync` / `StreamSeriesAsync` |
| `get_vod_info` | `GetVodInfoAsync` |
| `get_series_info` | `GetSeriesInfoAsync` |
| `get_short_epg` | `GetShortEpgAsync` |
| `get_simple_data_table` | `GetEpgAsync` |
| `get_catchup_table` | `GetCatchupTableAsync` |
| `xmltv.php` | `OpenXmltvAsync` |
| `get.php` | `OpenPlaylistAsync` |

Playback URLs for live, movies, episodes and catch-up come from
`XtreamStreamUrlBuilder`.

## Error handling

Four distinct failures, because the caller needs to know whether to fix the
credentials or check the network:

```csharp
try
{
    var account = await client.AuthenticateAsync(cancellationToken);
}
catch (XtreamAuthenticationException)      { /* credentials rejected */ }
catch (XtreamConnectionException ex)       { /* unreachable; ex.IsTimeout */ }
catch (XtreamHttpException ex)             { /* server error; ex.IsTransient */ }
catch (XtreamProtocolException ex)         { /* unreadable; ex.ResponseSnippet */ }
```

`XtreamProtocolException.ResponseSnippet` keeps the first bytes of what the
server actually sent. Panels frequently answer `200 OK` with an HTML error page,
and without that snippet the incident cannot be diagnosed.

## The User-Agent matters

Many panels reject a request whose User-Agent they do not recognise, answering
`401` on perfectly valid credentials. This is the first thing to change when a
connection fails for no apparent reason:

```csharp
using var client = new XtreamClient(credentials, new XtreamClientOptions
{
    UserAgent = "VLC/3.0.20 LibVLC/3.0.20",
});
```

## Response progress

The transport reports how much of an API response has arrived, which is worth
surfacing on large catalogues:

```csharp
using var transport = new XtreamHttpTransport(options);
transport.Progress += (_, p) => Console.WriteLine(p.Percentage is { } pct
    ? $"{pct:0.00} %"
    : $"{p.BytesReceived} bytes");

using var client = new XtreamClient(credentials, transport);
```

`Percentage` is `null` when the server does not announce a size — either because
it replies in chunks, or because it compressed the response, in which case .NET
drops the length header while decompressing. `BytesReceived` is always accurate.

## Design notes

- **Timeouts are applied in two phases.** `HttpClient.Timeout` bounds the whole
  request, body included, so a global value would cut off the legitimate
  transfer of a large catalogue. The transport times out the response *headers*
  only; the body is bounded by the caller's cancellation token.
- **Credentials are URL-encoded** everywhere, and masked in every string that
  might reach a log. A password containing `&` silently breaks any URL built by
  concatenation.
- **Authentication is not just `auth == 1`.** Panels return `auth: 1` on expired
  accounts; `UserInfo.IsUsable` checks authentication, status and expiry.

`docs/xtream-api-reference.md` documents the protocol itself: every action, its
parameters, the typing traps, and the playback URL formats.

## Building

```bash
dotnet build
dotnet test
```

## Credits

The overall shape — the `Client` / `Transport` / `Credentials` split, and the
configurable HTTP client factory — was inspired by
[Fazzani/Xtream.Client](https://github.com/Fazzani/Xtream.Client) (MIT). No
source file is copied from it; see `NOTICE`.

## License

MIT — see `LICENSE`. The license disclaims all warranty and all liability; see
also **Intended use and disclaimer** above.

---

All source comments and XML documentation are in English.
