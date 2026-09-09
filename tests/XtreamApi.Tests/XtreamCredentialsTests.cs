namespace XtreamApi.Tests;

public class XtreamCredentialsTests
{
    [Fact]
    public void Extracts_credentials_from_a_get_php_url()
    {
        var credentials = XtreamCredentials.FromPlaylistUrl(
            "http://panel.example.com:8080/get.php?username=demo&password=secret&type=m3u_plus&output=ts");

        Assert.Equal("http://panel.example.com:8080/", credentials.BaseAddress.ToString());
        Assert.Equal("demo", credentials.Username);
        Assert.Equal("secret", credentials.Password);
    }

    [Theory]
    // The characters that defeat a regular-expression parse.
    [InlineData("p%40ssw0rd", "p@ssw0rd")]
    [InlineData("a%26b", "a&b")]
    [InlineData("mot%20de%20passe", "mot de passe")]
    [InlineData("50%25", "50%")]
    [InlineData("tiret-bas_et.point", "tiret-bas_et.point")]
    [InlineData("cle%3Dvaleur", "cle=valeur")]
    public void Decodes_a_password_with_special_characters(string encoded, string expected)
    {
        var credentials = XtreamCredentials.FromPlaylistUrl(
            $"http://panel.example.com/get.php?username=demo&password={encoded}");

        Assert.Equal(expected, credentials.Password);
    }

    [Fact]
    public void The_plus_sign_is_kept_as_is_in_the_password()
    {
        // A deliberate choice: panels build these URLs from the raw credentials,
        // where a literal "+" is more likely than a space.
        var credentials = XtreamCredentials.FromPlaylistUrl(
            "http://panel.example.com/get.php?username=demo&password=a+b");

        Assert.Equal("a+b", credentials.Password);
    }

    [Fact]
    public void Accepts_a_player_api_url_as_the_source()
    {
        var credentials = XtreamCredentials.FromPlaylistUrl(
            "https://panel.example.com:8443/player_api.php?username=demo&password=secret");

        Assert.Equal("https://panel.example.com:8443/", credentials.BaseAddress.ToString());
        Assert.Equal("demo", credentials.Username);
    }

    [Fact]
    public void The_order_of_the_parameters_does_not_matter()
    {
        var credentials = XtreamCredentials.FromPlaylistUrl(
            "http://panel.example.com/get.php?type=m3u_plus&password=secret&username=demo");

        Assert.Equal("demo", credentials.Username);
        Assert.Equal("secret", credentials.Password);
    }

    [Fact]
    public void The_default_port_does_not_appear_in_the_base_address()
    {
        var credentials = XtreamCredentials.FromPlaylistUrl(
            "http://panel.example.com/get.php?username=demo&password=secret");

        Assert.Equal("http://panel.example.com/", credentials.BaseAddress.ToString());
    }

    [Theory]
    [InlineData("http://panel.example.com/get.php?username=demo")]
    [InlineData("http://panel.example.com/get.php?password=secret")]
    [InlineData("http://panel.example.com/get.php")]
    [InlineData("not a url")]
    [InlineData("ftp://panel.example.com/get.php?username=demo&password=secret")]
    public void An_unusable_url_is_rejected(string url)
    {
        Assert.Throws<XtreamUrlFormatException>(() => XtreamCredentials.FromPlaylistUrl(url));
        Assert.False(XtreamCredentials.TryFromPlaylistUrl(url, out _));
        Assert.False(XtreamCredentials.LooksLikeXtreamPlaylist(url));
    }

    [Fact]
    public void TryFromPlaylistUrl_returns_credentials_when_the_url_is_suitable()
    {
        Assert.True(XtreamCredentials.TryFromPlaylistUrl(
            "http://panel.example.com/get.php?username=demo&password=secret",
            out var credentials));

        Assert.Equal("demo", credentials.Username);
    }

    [Theory]
    [InlineData("panel.example.com", "http://panel.example.com/")]
    [InlineData("panel.example.com:8080", "http://panel.example.com:8080/")]
    [InlineData("http://panel.example.com:8080", "http://panel.example.com:8080/")]
    [InlineData("https://panel.example.com", "https://panel.example.com/")]
    [InlineData("http://panel.example.com:8080/", "http://panel.example.com:8080/")]
    // A path pasted by the user must not contaminate subsequent calls.
    [InlineData("http://panel.example.com:8080/get.php", "http://panel.example.com:8080/")]
    public void Normalises_the_server_address(string server, string expected)
    {
        var credentials = XtreamCredentials.Create(server, "demo", "secret");

        Assert.Equal(expected, credentials.BaseAddress.ToString());
    }

    [Fact]
    public void An_empty_server_address_is_refused()
    {
        Assert.Throws<ArgumentException>(() => XtreamCredentials.Create("   ", "demo", "secret"));
    }

    [Fact]
    public void ToString_masks_the_password()
    {
        // These objects end up in logs and in incident reports.
        var credentials = XtreamCredentials.Create("panel.example.com", "demo", "tres-secret");

        var text = credentials.ToString();

        Assert.DoesNotContain("tres-secret", text, StringComparison.Ordinal);
        Assert.Contains("demo", text, StringComparison.Ordinal);
    }
}
