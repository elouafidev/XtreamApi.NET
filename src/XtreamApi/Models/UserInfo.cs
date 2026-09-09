using System.Text.Json.Serialization;
using XtreamApi.Serialization.Converters;

namespace XtreamApi.Models;

/// <summary>
/// The <c>user_info</c> block returned by <c>player_api.php</c>.
/// </summary>
public sealed class UserInfo
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    /// <summary>Free-text message from the panel, often a reseller advert.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>1 when the credentials are recognised, 0 otherwise.</summary>
    [JsonPropertyName("auth")]
    public int Auth { get; init; }

    /// <summary>
    /// Raw status ("Active", "Expired", "Disabled", "Banned"). Kept verbatim so
    /// the caller can show what the panel actually said, even when the value is
    /// not one of the known cases.
    /// </summary>
    [JsonPropertyName("status")]
    public string? StatusText { get; init; }

    /// <summary>Expiry date. <c>null</c> means the subscription has no end date.</summary>
    [JsonPropertyName("exp_date")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTimeOffset? ExpiresAt { get; init; }

    [JsonPropertyName("is_trial")]
    public bool IsTrial { get; init; }

    /// <summary>Connections currently open on this account.</summary>
    [JsonPropertyName("active_cons")]
    public int ActiveConnections { get; init; }

    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Concurrent connections allowed. 0 when the panel does not say.</summary>
    [JsonPropertyName("max_connections")]
    public int MaxConnections { get; init; }

    /// <summary>Containers available for live streams: <c>ts</c>, <c>m3u8</c>, <c>rtmp</c>.</summary>
    [JsonPropertyName("allowed_output_formats")]
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public IReadOnlyList<string> AllowedOutputFormats { get; init; } = [];

    /// <summary>The panel recognised the credentials.</summary>
    [JsonIgnore]
    public bool IsAuthenticated => Auth == 1;

    /// <summary><see cref="StatusText"/>, interpreted.</summary>
    [JsonIgnore]
    public AccountStatus Status => StatusText?.Trim().ToLowerInvariant() switch
    {
        "active" => AccountStatus.Active,
        "expired" => AccountStatus.Expired,
        "disabled" => AccountStatus.Disabled,
        "banned" => AccountStatus.Banned,
        _ => AccountStatus.Unknown,
    };

    /// <summary>The subscription is past its expiry date.</summary>
    [JsonIgnore]
    public bool IsExpired =>
        Status == AccountStatus.Expired || (ExpiresAt is not null && ExpiresAt <= DateTimeOffset.UtcNow);

    /// <summary>
    /// The account is usable right now: credentials recognised, status active
    /// and expiry date not reached.
    /// <para>
    /// Not to be confused with <see cref="ActiveConnections"/>, which counts open
    /// connections. Conflating the two marks as inactive every valid account
    /// that simply nobody is watching.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public bool IsUsable => IsAuthenticated && Status != AccountStatus.Disabled
        && Status != AccountStatus.Banned && !IsExpired;

    /// <summary>Subscription with no end date.</summary>
    [JsonIgnore]
    public bool IsUnlimited => ExpiresAt is null;

    /// <summary>Time left before expiry, <c>null</c> when there is no end date.</summary>
    [JsonIgnore]
    public TimeSpan? TimeRemaining =>
        ExpiresAt is null ? null : ExpiresAt.Value - DateTimeOffset.UtcNow;

    /// <summary>Connections still available, <c>null</c> when the panel publishes no limit.</summary>
    [JsonIgnore]
    public int? AvailableConnections =>
        MaxConnections <= 0 ? null : Math.Max(0, MaxConnections - ActiveConnections);
}
