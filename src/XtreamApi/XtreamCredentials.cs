using System.Diagnostics.CodeAnalysis;

namespace XtreamApi;

/// <summary>
/// Access details for an Xtream panel: server address and credentials.
/// </summary>
public sealed class XtreamCredentials
{
    private XtreamCredentials(Uri baseAddress, string username, string password)
    {
        BaseAddress = baseAddress;
        Username = username;
        Password = password;
    }

    /// <summary>Root of the panel, without path or query string.</summary>
    public Uri BaseAddress { get; }

    public string Username { get; }

    public string Password { get; }

    /// <summary>
    /// Builds credentials from a server address and a username / password
    /// pair.
    /// </summary>
    /// <param name="server">
    /// Address of the panel. The scheme is optional (<c>http</c> by default) and
    /// any path is ignored.
    /// </param>
    /// <param name="username">Account username.</param>
    /// <param name="password">Account password.</param>
    public static XtreamCredentials Create(string server, string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        return new XtreamCredentials(NormalizeServer(server), username, password);
    }

    /// <summary>
    /// Extracts credentials from a playlist URL
    /// (<c>get.php</c>, <c>player_api.php</c>, <c>panel_api.php</c>...).
    /// <para>
    /// Parsing goes through <see cref="Uri"/> and an explicit decode of the query
    /// string rather than a regular expression: passwords containing <c>%</c>,
    /// <c>&amp;</c> or a dash are common among resellers and defeat any
    /// pattern-based approach.
    /// </para>
    /// </summary>
    /// <exception cref="XtreamUrlFormatException">
    /// The URL is invalid or carries no username / password pair.
    /// </exception>
    public static XtreamCredentials FromPlaylistUrl(string playlistUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistUrl);

        if (!Uri.TryCreate(playlistUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new XtreamUrlFormatException(
                "The playlist URL must be an absolute http or https address.");
        }

        var query = ParseQuery(uri.Query);

        if (!query.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username))
        {
            throw new XtreamUrlFormatException(
                "The playlist URL carries no 'username' parameter.");
        }

        // Panels reject an empty password, but the parameter may legitimately be
        // present and empty: only its absence is rejected here.
        if (!query.TryGetValue("password", out var password))
        {
            throw new XtreamUrlFormatException(
                "The playlist URL carries no 'password' parameter.");
        }

        return new XtreamCredentials(BuildRoot(uri), username, password);
    }

    /// <summary>
    /// Exception-free variant of <see cref="FromPlaylistUrl"/>.
    /// </summary>
    public static bool TryFromPlaylistUrl(
        string? playlistUrl,
        [NotNullWhen(true)] out XtreamCredentials? credentials)
    {
        credentials = null;

        if (string.IsNullOrWhiteSpace(playlistUrl))
        {
            return false;
        }

        try
        {
            credentials = FromPlaylistUrl(playlistUrl);
            return true;
        }
        catch (XtreamUrlFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Tells whether a URL looks like an Xtream playlist, without fully
    /// validating it. Useful to guide the user while they type.
    /// </summary>
    public static bool LooksLikeXtreamPlaylist(string? url) => TryFromPlaylistUrl(url, out _);

    /// <summary>
    /// Representation intended for logs: the password is masked.
    /// </summary>
    public override string ToString() => $"{BaseAddress}{Username}:***";

    private static Uri NormalizeServer(string server)
    {
        var text = server.Trim();

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "http://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new XtreamUrlFormatException($"Invalid server address: '{server}'.");
        }

        return BuildRoot(uri);
    }

    /// <summary>
    /// Keeps only scheme, host and port: credentials carried in the path or the
    /// query string must not be replayed into subsequent calls.
    /// </summary>
    private static Uri BuildRoot(Uri uri)
    {
        var builder = new UriBuilder(uri.Scheme, uri.Host);

        if (!uri.IsDefaultPort)
        {
            builder.Port = uri.Port;
        }

        return builder.Uri;
    }

    /// <summary>
    /// Splits a query string.
    /// <para>
    /// <c>+</c> is left as-is rather than turned into a space: playlist URLs are
    /// produced by panels from the raw credentials, and a password containing a
    /// literal <c>+</c> is far more common than one containing a space.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);

            // First occurrence wins: a repeated parameter is a provider mistake,
            // not a list.
            values.TryAdd(key, value);
        }

        return values;
    }
}
