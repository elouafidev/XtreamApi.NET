using System.Text;

namespace XtreamApi.Http;

/// <summary>
/// Builds the call URLs from credentials and a request.
/// </summary>
public static class XtreamUrlBuilder
{
    private const string PasswordMask = "***";

    /// <summary>
    /// Builds the complete URL of the call.
    /// <para>
    /// Every value goes through <see cref="Uri.EscapeDataString(string)"/>. This
    /// is essential: a password containing <c>&amp;</c>, <c>+</c>, <c>%</c> or
    /// <c>#</c> -- common currency among resellers -- silently breaks any URL
    /// assembled by concatenation.
    /// </para>
    /// </summary>
    public static Uri Build(XtreamCredentials credentials, XtreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);

        return new Uri(credentials.BaseAddress, BuildRelative(credentials, request, maskPassword: false));
    }

    /// <summary>
    /// The same URL with the password masked. To be used in logs and error
    /// messages: an Xtream URL carries the credentials in clear text and would
    /// otherwise end up in the traces.
    /// </summary>
    public static Uri BuildRedacted(XtreamCredentials credentials, XtreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);

        return new Uri(credentials.BaseAddress, BuildRelative(credentials, request, maskPassword: true));
    }

    private static string BuildRelative(XtreamCredentials credentials, XtreamRequest request, bool maskPassword)
    {
        var builder = new StringBuilder(ScriptName(request.Endpoint));

        builder.Append("?username=").Append(Uri.EscapeDataString(credentials.Username));
        builder.Append("&password=")
            .Append(maskPassword ? PasswordMask : Uri.EscapeDataString(credentials.Password));

        if (!string.IsNullOrEmpty(request.Action))
        {
            builder.Append("&action=").Append(Uri.EscapeDataString(request.Action));
        }

        foreach (var (name, value) in request.Parameters)
        {
            builder.Append('&')
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }

        if (request.ExpectsJson)
        {
            // Older panels answer in XML by default. Other panels ignore this
            // parameter, so it is harmless.
            builder.Append("&format=json");
        }

        return builder.ToString();
    }

    private static string ScriptName(XtreamEndpoint endpoint) => endpoint switch
    {
        XtreamEndpoint.PlayerApi => "player_api.php",
        XtreamEndpoint.PanelApi => "panel_api.php",
        XtreamEndpoint.Xmltv => "xmltv.php",
        XtreamEndpoint.Playlist => "get.php",
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint."),
    };
}
