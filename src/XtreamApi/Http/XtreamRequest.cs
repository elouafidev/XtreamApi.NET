namespace XtreamApi.Http;

/// <summary>
/// Target script on the panel.
/// </summary>
public enum XtreamEndpoint
{
    /// <summary>
    /// <c>player_api.php</c>: every catalogue and EPG action.
    /// </summary>
    PlayerApi = 0,

    /// <summary>
    /// <c>panel_api.php</c>: returns everything at once, in a non-standard
    /// shape. Kept for older panels, best avoided otherwise.
    /// </summary>
    PanelApi,

    /// <summary>
    /// <c>xmltv.php</c>: the full guide. Returns XML, never JSON.
    /// </summary>
    Xmltv,

    /// <summary>
    /// <c>get.php</c>: the M3U playlist. Returns text, never JSON.
    /// </summary>
    Playlist,
}

/// <summary>
/// Description of an API call, independent of the transport.
/// </summary>
public sealed record XtreamRequest
{
    private XtreamRequest(XtreamEndpoint endpoint, string? action, IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        Endpoint = endpoint;
        Action = action;
        Parameters = parameters;
    }

    public XtreamEndpoint Endpoint { get; }

    /// <summary>Value of the <c>action</c> parameter, <c>null</c> for the sign-in call.</summary>
    public string? Action { get; }

    /// <summary>Additional parameters, excluding credentials and action.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Parameters { get; }

    /// <summary>The expected response is JSON.</summary>
    public bool ExpectsJson => Endpoint is XtreamEndpoint.PlayerApi or XtreamEndpoint.PanelApi;

    /// <summary>
    /// Call without an action: state of the account and of the server.
    /// </summary>
    public static XtreamRequest Account() => new(XtreamEndpoint.PlayerApi, null, []);

    /// <summary>
    /// A <c>player_api.php</c> action, with its optional parameters.
    /// </summary>
    public static XtreamRequest ForAction(string action, params ReadOnlySpan<(string Name, string Value)> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var collected = new List<KeyValuePair<string, string>>(parameters.Length);
        foreach (var (name, value) in parameters)
        {
            collected.Add(new KeyValuePair<string, string>(name, value));
        }

        return new XtreamRequest(XtreamEndpoint.PlayerApi, action, collected);
    }

    /// <summary>Call to <c>panel_api.php</c>.</summary>
    public static XtreamRequest Panel() => new(XtreamEndpoint.PanelApi, null, []);

    /// <summary>Full guide in XMLTV form.</summary>
    public static XtreamRequest Xmltv() => new(XtreamEndpoint.Xmltv, null, []);

    /// <summary>M3U playlist.</summary>
    public static XtreamRequest Playlist(string type = "m3u_plus", string output = "ts") =>
        new(
            XtreamEndpoint.Playlist,
            null,
            [new KeyValuePair<string, string>("type", type), new KeyValuePair<string, string>("output", output)]);
}
