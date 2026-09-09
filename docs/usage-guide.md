# XtreamApi — Usage Guide

*[Version française](usage-guide-fr.md)*

Everything the library offers, and how to put it to work in a real
application. It assumes you have read the disclaimer in the `README`, and that
the panel you point it at is one you are lawfully entitled to query.

For the protocol itself — the raw actions, their parameters and their quirks —
see [`xtream-api-reference.md`](xtream-api-reference.md). This document is
about the C# surface.

---

## Contents

1. [Scope of the library](#1-scope-of-the-library)
2. [Requirements and installation](#2-requirements-and-installation)
3. [The four building blocks](#3-the-four-building-blocks)
4. [First connection](#4-first-connection)
5. [Feature reference](#5-feature-reference)
6. [Building playback addresses](#6-building-playback-addresses)
7. [Configuration](#7-configuration)
8. [Progress reporting](#8-progress-reporting)
9. [Error handling](#9-error-handling)
10. [Integration patterns](#10-integration-patterns)
11. [Lifetime, threading and cancellation](#11-lifetime-threading-and-cancellation)
12. [Format tolerance](#12-format-tolerance)
13. [Extending the library](#13-extending-the-library)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Scope of the library

XtreamApi is a **protocol client**. It speaks the Xtream Codes panel API over
HTTP, deserialises the answers into typed objects, and assembles the addresses
a media player needs.

**What it does**

- Authenticates against a panel and reports the state of the account.
- Reads the catalogue: categories, live channels, movies, series, episodes.
- Reads the electronic programme guide, including catch-up windows.
- Opens the XMLTV guide and the M3U playlist as streams.
- Builds playback addresses for live channels, movies, episodes and replays.

**What it deliberately does not do**

- It **ships no content, no channel list, no server address and no
  credentials.** Every one of those comes from you at run time.
- It **does not discover panels**, does not probe addresses and embeds no
  directory of providers.
- It **never writes media to disk.** The library reads the API and produces
  URLs; nothing more. If a consuming application saves a stream — a personal
  recording of a programme you are entitled to watch — that is the
  application's own feature, its own code, and its own responsibility.
- It **circumvents no protection.** There is no DRM handling, no token
  forging, no rate-limit evasion. The only header it lets you set is the
  `User-Agent`, because many panels reject requests that carry none.

Keep this boundary in mind when you build on top of it: the library's job ends
at *"here is the typed answer, and here is the address"*.

---

## 2. Requirements and installation

| | |
| --- | --- |
| Target frameworks | `net8.0`, `net10.0` |
| Dependencies | none — `System.Text.Json` and `System.Net.Http` only |
| Language level | C# `latest`, nullable reference types enabled |

```bash
dotnet add package XtreamApi
```

Or, from source, reference the project directly:

```xml
<ProjectReference Include="..\XtreamApi.NET\src\XtreamApi\XtreamApi.csproj" />
```

`net8.0` is the floor on purpose: an application on .NET 8, 9, 10 or later can
consume the package. The `net10.0` target exists so the package advertises
current support explicitly.

---

## 3. The four building blocks

```
XtreamCredentials ──► XtreamClient ──► XtreamStreamUrlBuilder
                           │
                           └──► IXtreamTransport (HTTP policy, progress)
```

**`XtreamCredentials`** — the address of the panel plus a username and a
password. Immutable, validated at construction, and its `ToString()` masks the
password so it can safely reach a log.

**`IXtreamTransport`** — the HTTP layer: timeouts, retries, headers,
deserialisation, progress. `XtreamHttpTransport` is the shipped
implementation. It is separate from the client so that network policy lives in
one place, and so tests can run without a server.

**`XtreamClient`** — the API surface: one method per Xtream action. It holds
the credentials, so they never appear in a method signature. Every method
reads; none of them writes to the panel.

**`XtreamStreamUrlBuilder`** — assembles playback addresses. These are *not*
returned by the API; they are built from the credentials and the item.

---

## 4. First connection

```csharp
using XtreamApi;

var credentials = XtreamCredentials.Create(
    "http://panel.example.com:8080", "username", "password");

using var client = new XtreamClient(credentials);

// Throws when the credentials are rejected, or the account is expired,
// disabled or banned.
var account = await client.AuthenticateAsync(cancellationToken);

Console.WriteLine($"Status      : {account.User?.Status}");
Console.WriteLine($"Expires     : {account.User?.ExpiresAt:d}");
Console.WriteLine($"Connections : {account.User?.ActiveConnections}/{account.User?.MaxConnections}");
Console.WriteLine($"Formats     : {string.Join(", ", account.User?.AllowedOutputFormats ?? [])}");
```

If all you have is a playlist address, the credentials can be extracted from
it:

```csharp
var credentials = XtreamCredentials.FromPlaylistUrl(url);

// Exception-free variants, useful while the user is still typing:
if (XtreamCredentials.TryFromPlaylistUrl(url, out var parsed)) { /* ... */ }
bool looksRight = XtreamCredentials.LooksLikeXtreamPlaylist(url);
```

Parsing goes through `Uri` and an explicit query decode rather than a regular
expression, because passwords containing `%`, `&` or `+` are common and defeat
any pattern-based approach. Only the scheme, host and port are kept: anything
carried in the path is dropped rather than replayed into later calls.

---

## 5. Feature reference

### 5.1 Account and server state

| Method | Behaviour |
| --- | --- |
| `GetAccountAsync` | Reports the panel's answer as-is. Does **not** throw on a refusal — the response simply carries `auth = 0`, which a sign-in screen may want to display. |
| `AuthenticateAsync` | Validates the connection. Throws `XtreamAuthenticationException` on refusal, expiry, suspension or a ban. |

Checking `auth == 1` alone is not enough, and this is a trap worth knowing:
**panels routinely answer `auth: 1` on a subscription that has already
ended.** `UserInfo.IsUsable` combines authentication, status and expiry, and
that is what `AuthenticateAsync` tests.

Useful members on `UserInfo`: `Status` (`Active`, `Expired`, `Disabled`,
`Banned`, `Unknown`), `IsAuthenticated`, `IsExpired`, `IsUsable`, `IsTrial`,
`ExpiresAt`, `CreatedAt`, `ActiveConnections`, `MaxConnections`,
`AllowedOutputFormats`.

On `ServerInfo`: `BaseAddress` (the streaming host, assembled from the
protocol, URL and port the panel declares), `TimeZone`, `TimeNow`,
`TimestampNow`, `HttpsPort`, `RtmpPort`.

> `ActiveConnections` against `MaxConnections` is worth surfacing in any user
> interface. Every simultaneous stream consumes one slot of the subscription's
> quota; going over it makes the next request fail for reasons the user will
> otherwise find inexplicable.

### 5.2 Categories

```csharp
var live   = await client.GetLiveCategoriesAsync(cancellationToken);
var movies = await client.GetVodCategoriesAsync(cancellationToken);
var series = await client.GetSeriesCategoriesAsync(cancellationToken);
```

Each `XtreamCategory` carries an `Id`, a `Name` and a `ParentId`.

### 5.3 Catalogues — buffered or streamed

Every catalogue comes in two shapes:

```csharp
// Buffered: the whole list, once it has all arrived.
IReadOnlyList<LiveStream>    channels = await client.GetLiveStreamsAsync(categoryId, ct);
IReadOnlyList<VodStream>     movies   = await client.GetVodStreamsAsync(categoryId, ct);
IReadOnlyList<SeriesSummary> series   = await client.GetSeriesAsync(categoryId, ct);

// Streamed: each item as it is parsed, without ever holding the whole
// response in memory.
await foreach (var channel in client.StreamLiveStreamsAsync(cancellationToken: ct))
{
    // fill a list view as the answer arrives
}
```

**Prefer the streaming form for unfiltered catalogues.** A single
`get_live_streams` answer from a large panel routinely exceeds several tens of
megabytes; the buffered call holds all of it, plus the parsed objects, at
once. Passing a `categoryId` narrows the answer at the source and is always
cheaper than filtering afterwards.

Common members, shared through the `CatalogItem` base type: `Num`, `Name`,
`CategoryId`, `CategoryIds`, `AddedAt`, `DirectSource`, `Kind`, `Id`.

`LiveStream` adds `StreamId`, `LogoUrl`, `EpgChannelId`, `IsAdult`,
`TvArchive`, `TvArchiveDuration` and the computed `HasCatchup`.

`VodStream` adds `StreamId`, `Title`, `Year`, `PosterUrl`, `Rating`,
`Rating5Based`, `ContainerExtension`, `TmdbId`, `Plot`.

`SeriesSummary` adds `SeriesId`, `Title`, `CoverUrl`, `Plot`, `Cast`,
`Director`, `Genre`, `BackdropPaths`, `EpisodeRunTime` and both release-date
spellings the panels use.

### 5.4 Details

```csharp
VodInfo?    movie  = await client.GetVodInfoAsync(streamId, ct);
SeriesInfo? season = await client.GetSeriesInfoAsync(seriesId, ct);
```

Both return `null` when the panel does not know the identifier, rather than
throwing: an unknown id is an ordinary outcome when a catalogue has been
refreshed underneath you.

`VodInfo` splits into `Details` (plot, cast, director, country, genre,
duration, rating, backdrops, trailer) and `MovieData` (`StreamId`, `Title`,
`Year`, `ContainerExtension`). The raw `Video` and `Audio` blocks are exposed
as `JsonElement`: their shape depends on the transcoder the provider runs, and
inventing a common model for them would be guesswork.

`SeriesInfo` carries `Details`, the list of `Seasons`, and `Episodes` as an
`IReadOnlyDictionary<int, IReadOnlyList<Episode>>` keyed by season number.
That dictionary is the one place the API's inconsistency shows through most: a
panel returns it as an array while the seasons are contiguous, then switches
to an object the moment a season is removed. The converter accepts both.

Each `Episode` has `EpisodeId` — **the identifier to play is the episode's,
never the series'** — plus `EpisodeNumber`, `Season`, `Title`,
`ContainerExtension` and a `Details` block with the plot, the duration, the
bitrate and the artwork.

### 5.5 Programme guide

```csharp
// The next few programmes on a channel.
var next = await client.GetShortEpgAsync(streamId, limit: 10, ct);

// The full table for one channel.
var full = await client.GetEpgAsync(streamId, ct);

// The whole guide, XMLTV, as a stream.
await using var xmltv = await client.OpenXmltvAsync(ct);
```

`GetEpgAsync` requires a stream identifier. Some clients call
`get_simple_data_table` without one and conclude the panel does not support
it; in fact it never answers anything useful that way.

`EpgListing` decodes the Base64 titles and descriptions panels send, and
exposes both the local times (`StartLocal`, `EndLocal`) and the epoch-based
ones (`StartTimestamp`, `StopTimestamp`), plus `Duration`, `NowPlaying` and
`HasArchive`.

`OpenXmltvAsync` returns a stream the caller disposes. Read it as a stream:
the full guide of a large panel can exceed a hundred megabytes, and buffering
it as a string is a reliable way to run out of memory.

### 5.6 Catch-up windows

```csharp
var replayable = await client.GetCatchupTableAsync(
    streamId,
    startInServerTime: serverNow.AddDays(-2),
    endInServerTime:   serverNow,
    cancellationToken: ct);
```

Support for this action, and the reading of its bounds, vary from panel to
panel — an empty list does not prove catch-up is unavailable.
`LiveStream.HasCatchup` (from `tv_archive` and `tv_archive_duration`) remains
the more reliable signal.

**The bounds are expressed in the server's time zone, not in UTC.** This is
the single most common source of programmes that replay one or two hours off.
`ServerInfo.TimeZone` tells you which zone to convert into.

### 5.7 M3U playlist

```csharp
await using var playlist = await client.OpenPlaylistAsync(
    type: "m3u_plus", output: "ts", cancellationToken: ct);
```

Returned as a stream, for the same reason as XMLTV. Useful when you are
handing the list to a player that expects a playlist rather than driving the
catalogue yourself.

---

## 6. Building playback addresses

Playback addresses are not part of any API answer. They are assembled:

```csharp
// Prefer this: the panel may serve streams from a host other than the one
// serving the API, and addresses built on the API host are then rejected.
var urls = client.CreateStreamUrlBuilder(account.Server);

Uri channel = urls.BuildLive(liveStream);                 // or (streamId, "ts")
Uri movie   = urls.BuildMovie(vodStream);                 // or (streamId, "mkv")
Uri episode = urls.BuildEpisode(episode);
Uri any     = urls.BuildFor(catalogItem);                 // dispatches on Kind

Uri replay  = urls.BuildCatchup(
    streamId,
    startInServerTime: start,      // server time zone, as above
    duration: TimeSpan.FromMinutes(90));
```

Three details the builder handles that hand-rolled concatenation does not:

- **`direct_source` wins when the provider sets it.** Some panels impose a
  specific address per item; the overloads that take a catalogue object honour
  it automatically.
- **Credentials are URL-encoded** in every segment. A password containing `/`,
  `?` or `#` would otherwise point the address somewhere else entirely.
- **The container extension is normalised.** Panels leave a leading dot in
  `container_extension` often enough to be worth handling once.

The format you ask for must appear in the account's
`AllowedOutputFormats` — typically `ts` for a continuous live stream and
`m3u8` for HLS.

---

## 7. Configuration

```csharp
var options = new XtreamClientOptions
{
    UserAgent               = "VLC/3.0.20 LibVLC/3.0.20",
    ConnectTimeout          = TimeSpan.FromSeconds(15),
    ResponseHeadersTimeout  = TimeSpan.FromSeconds(30),
    RetryCount              = 2,
    RetryBaseDelay          = TimeSpan.FromMilliseconds(500),
    MaxConnectionsPerServer = 8,
    ErrorSnippetLength      = 512,
    AllowInvalidCertificates = false,
};

using var client = new XtreamClient(credentials, options);
```

| Option | Default | Why you would change it |
| --- | --- | --- |
| `UserAgent` | `XtreamApi/1.0` | **The first thing to try when a valid account is refused.** Many panels answer `401` to a request whose agent they do not recognise, or accept only a short list of known ones. |
| `ConnectTimeout` | 15 s | TCP and TLS establishment only. |
| `ResponseHeadersTimeout` | 30 s | Bounds the wait for *headers*, never the body — see below. |
| `RetryCount` | 2 | Retries a transient failure: outage, timeout, `5xx`, `429`. Never a credential refusal. |
| `RetryBaseDelay` | 500 ms | Doubled on each further attempt. |
| `MaxConnectionsPerServer` | 8 | API calls do not normally consume the subscription's stream quota, but some panels count everything. Lower it if a panel starts refusing calls. |
| `ErrorSnippetLength` | 512 | How much of a bad answer is kept for diagnosis. |
| `AllowInvalidCertificates` | `false` | Only at the user's explicit request. It disables verification of the server's identity and exposes the connection to interception. Many panels do use self-signed or expired certificates, which is exactly why this must stay a deliberate choice. |

**Timeouts are applied in two phases, on purpose.** `HttpClient.Timeout`
bounds the whole request, body included, so a single global value would abort
the perfectly legitimate transfer of a large catalogue partway through. The
transport therefore times out the *headers* only; reading the body is bounded
by the caller's `CancellationToken`, which is where that decision belongs.

---

## 8. Progress reporting

The transport reports how much of an answer has arrived — worth surfacing on
large catalogues, where a user otherwise stares at a frozen window.

```csharp
using var transport = new XtreamHttpTransport(options);

transport.Progress += (_, p) => Console.WriteLine(
    p.Percentage is { } pct ? $"{pct:0.00} %" : $"{p.BytesReceived:N0} bytes");

using var client = new XtreamClient(credentials, transport, ownsTransport: true);
```

`XtreamProgress` gives `BytesReceived`, `TotalBytes`, `Fraction`, `Percentage`
and `IsMeasurable`.

Two things to know before you bind it to a progress bar:

- **`Percentage` is `null` when the server announces no size** — either
  because it answers in chunks, or because it compressed the answer, in which
  case .NET drops the length header while decompressing (the announced value
  covers the compressed bytes and would not match what is read). Fall back to
  `BytesReceived`, which is always accurate, or show an indeterminate bar.
- **The event is raised on the thread reading the answer, not the caller's.**
  A subscriber that touches a user interface must marshal back to it — in
  WinForms, `Control.BeginInvoke`. The event also does not distinguish between
  calls: two requests running side by side interleave their progress.

---

## 9. Error handling

Four distinct failures, because "something went wrong" does not tell a user
whether to fix their password or check their connection:

```csharp
try
{
    var account = await client.AuthenticateAsync(ct);
}
catch (XtreamAuthenticationException ex)
{
    // Credentials rejected, or account expired / disabled / banned.
    // ex.StatusCode is null when the refusal came from the answer body.
}
catch (XtreamConnectionException ex)
{
    // Unreachable: DNS, TLS, network. ex.IsTimeout separates a timeout from
    // an outright refusal. ex.RequestUri is masked.
}
catch (XtreamHttpException ex)
{
    // Error status. ex.IsTransient is true for 429 and 5xx — worth retrying.
}
catch (XtreamProtocolException ex)
{
    // The answer arrived but is unusable. ex.ResponseSnippet holds its first
    // bytes.
}
```

All four derive from `XtreamException`, so a single `catch` still works where
the distinction does not matter. `XtreamUrlFormatException` completes the set
for a malformed address.

`ResponseSnippet` earns its place: **panels very often answer `200 OK` with an
HTML error page in the body.** Without the first bytes of what actually
arrived, that incident is undiagnosable from a bug report.

---

## 10. Integration patterns

### 10.1 One-off use

```csharp
using var client = new XtreamClient(credentials);
```

The client creates and owns its transport. Convenient for a script or a probe.

### 10.2 A long-lived application

Create **one transport for the whole application** and hand it to every
client. Each transport builds its own `HttpClient`, and creating one per call
exhausts sockets even with a correct `Dispose` — the connections linger in
`TIME_WAIT`.

```csharp
// Once, at startup.
var transport = new XtreamHttpTransport(options);

// Per account, as often as needed. ownsTransport stays false: the shared
// transport outlives every client built on it.
using var client = new XtreamClient(credentials, transport, ownsTransport: false);
```

### 10.3 Dependency injection

```csharp
services.AddSingleton(new XtreamClientOptions { UserAgent = "..." });
services.AddSingleton<IXtreamTransport>(sp =>
    new XtreamHttpTransport(sp.GetRequiredService<XtreamClientOptions>()));

// Credentials are per user session, so the client is built where they are
// known rather than registered globally.
services.AddScoped<IXtreamClient>(sp => new XtreamClient(
    CredentialsForCurrentUser(sp),
    sp.GetRequiredService<IXtreamTransport>(),
    ownsTransport: false));
```

With `IHttpClientFactory`, use the transport overload that accepts an existing
`HttpClient` — but make sure that client keeps
`Timeout = Timeout.InfiniteTimeSpan`, or the two-phase timeout described in
§7 is defeated and large catalogues will be cut off.

### 10.4 A desktop application

Three habits that save trouble:

- Build the URL builder from `account.Server`, not from the credentials
  alone, right after sign-in — and keep it for the session.
- Fill lists from the `Stream*Async` methods so the window populates as the
  answer arrives instead of freezing until it is whole.
- Marshal `Progress` back to the UI thread (§8).

Storing credentials is the application's responsibility, and the library takes
no position beyond one thing: never keep a password in plain text. On Windows,
`ProtectedData` (DPAPI) ties the stored value to the user account for a few
lines of code.

---

## 11. Lifetime, threading and cancellation

**Disposal.** `XtreamClient` disposes its transport only when it owns it —
the single-argument constructor, or `ownsTransport: true`. A shared transport
must outlive every client built on it and be disposed at shutdown.

**Threading.** `XtreamClient` holds no mutable state and is safe to use from
several tasks at once, within the transport's connection limit. `Progress` is
raised on the reading thread.

**Cancellation.** Every method takes a `CancellationToken`, and it is the only
bound on reading a response body. Pass a real one: a window that closes while
a large catalogue is arriving should cancel it, not wait it out.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var channels = await client.GetLiveStreamsAsync(cancellationToken: cts.Token);
```

---

## 12. Format tolerance

Xtream panels are wildly inconsistent, and a client that reads them strictly
fails on the first oddity — taking the whole answer down for a field the
caller never asked about. Tolerance is the point of this library, and it is
covered by 171 tests written from payloads real panels return.

| What arrives | How it is handled |
| --- | --- |
| A number where a string is declared (`tmdb_id`, ids) | Read as text, preserving the exact digits — no float rounding. |
| A string where a number is declared (`"1080"`, `"7.5"`) | Parsed with the invariant culture. |
| `""`, `"0000-00-00"`, `"null"` for a date | Yields `null` rather than throwing. |
| Unix epochs, `yyyy-MM-dd HH:mm:ss`, ISO 8601 | All accepted for the same field. |
| Base64 EPG titles and descriptions | Decoded transparently. |
| An array that becomes an object once an entry is removed | Both accepted (seasons, category lists). |
| A single value where a list is declared | Wrapped into a one-item list. |
| `null` inside a collection | Skipped instead of aborting the parse. |

`XtreamJson.Default` exposes the configured `JsonSerializerOptions` if you
need to parse a raw payload yourself with the same tolerance.

---

## 13. Extending the library

**Replace the transport.** `IXtreamTransport` has four methods —
`GetJsonAsync`, `StreamJsonArrayAsync`, `GetTextAsync`, `OpenReadAsync` —
plus the `Progress` event. Implement it to test without a server, to add a
caching layer in front of the panel, or to log every call:

```csharp
public sealed class CachingTransport(IXtreamTransport inner) : IXtreamTransport
{
    public event EventHandler<XtreamProgress>? Progress
    {
        add    => inner.Progress += value;
        remove => inner.Progress -= value;
    }

    public Task<T?> GetJsonAsync<T>(
        XtreamCredentials credentials, XtreamRequest request, CancellationToken ct)
        => /* consult the cache, else inner.GetJsonAsync<T>(...) */;

    // ...
}
```

Caching is worth considering for categories, which change rarely and are asked
for on every start.

**Call an action the client does not wrap.** `XtreamRequest.ForAction` builds
any `player_api.php` call, and the transport will carry it:

```csharp
var request = XtreamRequest.ForAction("some_action", ("param", "value"));
var result  = await transport.GetJsonAsync<JsonElement>(credentials, request, ct);
```

**Swap the client out in tests.** Every method sits on `IXtreamClient`, so a
view model can be tested against a stub with no HTTP involved at all.

---

## 14. Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| `401` on credentials that work elsewhere | The `User-Agent`. Set one the panel accepts — this is the most frequent cause by a wide margin. |
| `AuthenticateAsync` throws, `GetAccountAsync` succeeds | Working as designed: the account authenticates but is expired, suspended or banned. Read `UserInfo.Status` and `ExpiresAt`. |
| `XtreamProtocolException` on a call that used to work | Read `ResponseSnippet`. It is usually an HTML error or maintenance page answered with `200 OK`. |
| Playback addresses are refused while the API answers fine | The panel streams from another host. Build the URLs from `account.Server`, not from the credentials. |
| A catch-up replay is one or two hours off | The bounds were passed as UTC. They must be in the server's zone — `ServerInfo.TimeZone`. |
| A large catalogue is cut off partway | A global `HttpClient.Timeout` was left in place on a supplied client. It must be `Timeout.InfiniteTimeSpan`. |
| Calls start failing after a few parallel requests | The panel is counting API calls against the connection quota. Lower `MaxConnectionsPerServer`. |
| A TLS error on a panel that a browser opens | Self-signed or expired certificate. `AllowInvalidCertificates` exists for it, but make it the user's explicit choice — it removes the guarantee that you are talking to the server you think you are. |
| Progress never shows a percentage | The server announced no size — chunked or compressed. Use `BytesReceived`, or an indeterminate bar. |

---

## See also

- [`usage-guide-fr.md`](usage-guide-fr.md) — the French version of this guide.
- [`xtream-api-reference.md`](xtream-api-reference.md) — the protocol: every
  action, its parameters, its typing traps and the address formats.
- `README.md` — installation, design notes, and the **Intended use and
  disclaimer** that governs everything above.
