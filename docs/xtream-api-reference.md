# Xtream Codes API reference

Working notes consolidated from several reference implementations (tuliprox,
py-xtream-codes, Xtream-Masters) and from inspecting existing clients. This
document is the specification `src/XtreamApi` is written against.

## 1. Entry point

```
{scheme}://{host}:{port}/player_api.php?username={user}&password={pass}[&action=...]
```

- Some older panels answer in XML by default: add `&format=json`.
- Many panels return **401** when the `User-Agent` is missing or unfamiliar. A
  configurable User-Agent is therefore mandatory on the client side.
- `panel_api.php` returns everything at once (user + server + categories +
  channels) but in a non-standard shape: an object indexed by number instead of
  an array. Avoid it; prefer targeted `player_api.php` calls.

## 2. Authentication

Call **without** an `action`:

```
GET /player_api.php?username=U&password=P
```

Response:

```json
{
  "user_info": {
    "username": "...", "password": "...",
    "auth": 1,                     // 0 = failure
    "status": "Active",            // Active | Expired | Disabled | Banned
    "exp_date": "1767225600",      // epoch seconds, may be null (unlimited)
    "is_trial": "0",
    "active_cons": "1",            // connections currently open
    "created_at": "1700000000",
    "max_connections": "2",
    "allowed_output_formats": ["m3u8", "ts", "rtmp"]
  },
  "server_info": {
    "url": "...", "port": "8080", "https_port": "8443",
    "server_protocol": "http", "rtmp_port": "1935",
    "timezone": "Europe/Paris",
    "timestamp_now": 1767225600, "time_now": "2026-01-01 00:00:00"
  }
}
```

Trap: `auth: 1` can coexist with `status: "Expired"`. **Both** must be checked
to validate a connection.

## 3. `player_api.php` actions

| Action | Parameters | Returns |
|---|---|---|
| *(none)* | - | `user_info` + `server_info` |
| `get_live_categories` | - | `[{category_id, category_name, parent_id}]` |
| `get_vod_categories` | - | same |
| `get_series_categories` | - | same |
| `get_live_streams` | `category_id` (opt.) | array of channels |
| `get_vod_streams` | `category_id` (opt.) | array of movies |
| `get_series` | `category_id` (opt.) | array of series |
| `get_vod_info` | `vod_id` **required** | `{info, movie_data}` |
| `get_series_info` | `series_id` **required** | `{info, seasons, episodes}` |
| `get_short_epg` | `stream_id` **required**, `limit` (opt., def. 4) | `{epg_listings:[...]}` |
| `get_simple_data_table` | `stream_id` **required** | `{epg_listings:[...]}`, the full guide for that channel |
| `get_catchup_table` | `stream_id` **required**, `start`, `end` (opt.) | catch-up window |

The full guide for every channel comes from
**`/xmltv.php?username=U&password=P`**, which returns **XML (XMLTV)**, not JSON.

### Typing traps

- Numeric fields come back sometimes as `number`, sometimes as `string`,
  inconsistently from one panel and one action to the next. The deserialiser
  must be tolerant (`JsonNumberHandling.AllowReadingFromString`).
- `title` and `description` of `epg_listings` are **Base64**-encoded.
- `exp_date`, `added` and `created_at` are epoch **seconds** carried as a
  `string`, sometimes `null`. Use `long`, never `int` (overflow in 2038).
- `category_id` is a `string` on streams and sometimes a `number` on categories.
- `get_series_info` returns `episodes` as an **object** keyed by season number
  (`{"1": [...], "2": [...]}`), not as an array.
- Fields that are textual by nature, such as `tmdb_id` or `year`, come out as
  numbers on some providers depending on whether the column is filled.

## 4. Building playback URLs

```
Live    : {scheme}://{host}:{port}/live/{user}/{pass}/{stream_id}.{ext}
Movie   : {scheme}://{host}:{port}/movie/{user}/{pass}/{stream_id}.{ext}
Episode : {scheme}://{host}:{port}/series/{user}/{pass}/{stream_id}.{ext}
```

- `ext` for live comes from `allowed_output_formats` (`ts` or `m3u8`).
- `ext` for VOD and series comes from `container_extension` /
  `target_container` on the item itself (`mkv`, `mp4`, ...).
- Some panels also accept `/{user}/{pass}/{stream_id}` with no prefix.
- When `direct_source` is set on the item, it can be used as-is.
- The host to build on is the one declared in `server_info`, which is not always
  the host that serves the API.

### Catch-up / timeshift

```
/timeshift/{user}/{pass}/{duration_min}/{start:yyyy-MM-dd:HH-mm}/{stream_id}.{ext}
/streaming/timeshift.php?username=U&password=P&stream={id}&start=...&duration=...
```

Available only when the channel has `tv_archive = 1`, over
`tv_archive_duration` days. The start time is expressed in the **server's** time
zone, never in UTC: comparing `time_now` with `timestamp_now` gives the offset
to apply.

## 5. Volume

Unfiltered, `get_live_streams` routinely returns **20 to 80 MB** of JSON from a
large provider. Consequences for any implementation:

- read as a **stream** (`HttpCompletionOption.ResponseHeadersRead` plus
  `JsonSerializer.DeserializeAsyncEnumerable`), never `GetStringAsync`;
- cache to disk locally;
- support real cancellation through a `CancellationToken`.
