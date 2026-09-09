using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// The <c>server_info</c> block returned by <c>player_api.php</c>.
/// </summary>
public sealed class ServerInfo
{
    /// <summary>Panel host, usually without scheme or port.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("port")]
    public int? Port { get; init; }

    [JsonPropertyName("https_port")]
    public int? HttpsPort { get; init; }

    /// <summary>"http" or "https".</summary>
    [JsonPropertyName("server_protocol")]
    public string? ServerProtocol { get; init; }

    [JsonPropertyName("rtmp_port")]
    public int? RtmpPort { get; init; }

    /// <summary>Server time zone, in IANA form ("Europe/Paris").</summary>
    [JsonPropertyName("timezone")]
    public string? TimeZone { get; init; }

    /// <summary>
    /// Server time as a Unix epoch. The source of truth, to be preferred over
    /// <see cref="TimeNow"/>, which is expressed in the server's own time zone.
    /// </summary>
    [JsonPropertyName("timestamp_now")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTimeOffset? TimestampNow { get; init; }

    /// <summary>Server time as text, in the server's own time zone.</summary>
    [JsonPropertyName("time_now")]
    [JsonConverter(typeof(LooseDateTimeOffsetConverter))]
    public DateTimeOffset? TimeNow { get; init; }

    [JsonPropertyName("process")]
    public bool? Process { get; init; }

    /// <summary>
    /// Base address to use when building playback URLs.
    /// <para>
    /// Rebuilt from a scheme and a port that agree with each other: over https
    /// it is <c>https_port</c> that counts, not <c>port</c>. The <c>url</c> field
    /// is accepted with or without a scheme, with or without a port attached.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public Uri? BaseAddress => BuildBaseAddress();

    private Uri? BuildBaseAddress()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            return null;
        }

        var host = Url.Trim();
        var scheme = string.IsNullOrWhiteSpace(ServerProtocol)
            ? Uri.UriSchemeHttp
            : ServerProtocol.Trim().ToLowerInvariant();

        if (host.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(host, UriKind.Absolute, out var absolute))
            {
                return null;
            }

            scheme = absolute.Scheme;
            host = absolute.Host;
        }

        // The port lives in a dedicated field; when it is also attached to the
        // host, the dedicated one wins so it stays consistent with the scheme.
        var portSeparator = host.IndexOf(':');
        if (portSeparator >= 0)
        {
            host = host[..portSeparator];
        }

        if (host.Length == 0)
        {
            return null;
        }

        var isHttps = string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var port = isHttps ? HttpsPort ?? Port : Port ?? HttpsPort;

        var builder = new UriBuilder(scheme, host);
        if (port is > 0 and <= 65535)
        {
            builder.Port = port.Value;
        }

        return builder.Uri;
    }
}
