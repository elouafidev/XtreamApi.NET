using System.Text.Json.Serialization;

namespace XtreamApi.Models;

/// <summary>
/// Response of <c>player_api.php</c> called without an action: the state of the
/// account and of the server.
/// </summary>
public sealed class XtreamAccount
{
    [JsonPropertyName("user_info")]
    public UserInfo? User { get; init; }

    [JsonPropertyName("server_info")]
    public ServerInfo? Server { get; init; }

    /// <summary>
    /// The account is usable. A panel may answer <c>auth = 1</c> on an expired
    /// subscription, so both conditions are checked.
    /// </summary>
    [JsonIgnore]
    public bool IsUsable => User?.IsUsable == true;
}
