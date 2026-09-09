using XtreamApi.Http;

namespace XtreamApi.Tests.Http;

public class XtreamUrlBuilderTests
{
    private static readonly XtreamCredentials Credentials =
        XtreamCredentials.Create("http://panel.example.com:8080", "demo", "secret");

    [Fact]
    public void The_sign_in_call_carries_no_action()
    {
        var uri = XtreamUrlBuilder.Build(Credentials, XtreamRequest.Account());

        Assert.Equal(
            "http://panel.example.com:8080/player_api.php?username=demo&password=secret&format=json",
            uri.ToString());
    }

    [Fact]
    public void An_action_without_parameters_is_added_to_the_query()
    {
        var uri = XtreamUrlBuilder.Build(Credentials, XtreamRequest.ForAction("get_live_streams"));

        Assert.Contains("action=get_live_streams", uri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_parameters_follow_the_action_in_the_given_order()
    {
        var uri = XtreamUrlBuilder.Build(
            Credentials,
            XtreamRequest.ForAction("get_short_epg", ("stream_id", "1234"), ("limit", "8")));

        Assert.Equal(
            "http://panel.example.com:8080/player_api.php"
            + "?username=demo&password=secret&action=get_short_epg&stream_id=1234&limit=8&format=json",
            uri.ToString());
    }

    [Theory]
    [InlineData("a&b", "a%26b")]
    [InlineData("p@ssw0rd", "p%40ssw0rd")]
    [InlineData("50%", "50%25")]
    [InlineData("a+b", "a%2Bb")]
    [InlineData("mot de passe", "mot%20de%20passe")]
    [InlineData("cle=valeur", "cle%3Dvaleur")]
    public void The_password_is_encoded_in_the_url(string password, string expected)
    {
        // Without encoding, an "&" in the password cuts the query in two and the
        // panel answers 401 on otherwise valid credentials.
        var credentials = XtreamCredentials.Create("http://panel.example.com", "demo", password);

        var uri = XtreamUrlBuilder.Build(credentials, XtreamRequest.Account());

        Assert.Contains($"password={expected}", uri.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public void The_username_is_encoded_as_well()
    {
        var credentials = XtreamCredentials.Create("http://panel.example.com", "de mo&x", "secret");

        var uri = XtreamUrlBuilder.Build(credentials, XtreamRequest.Account());

        Assert.Contains("username=de%20mo%26x", uri.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public void The_redacted_variant_hides_the_password()
    {
        var uri = XtreamUrlBuilder.BuildRedacted(Credentials, XtreamRequest.ForAction("get_live_streams"));

        Assert.DoesNotContain("secret", uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("password=***", uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("username=demo", uri.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(XtreamEndpoint.PlayerApi), "player_api.php")]
    [InlineData(nameof(XtreamEndpoint.PanelApi), "panel_api.php")]
    [InlineData(nameof(XtreamEndpoint.Xmltv), "xmltv.php")]
    [InlineData(nameof(XtreamEndpoint.Playlist), "get.php")]
    public void Each_endpoint_targets_its_own_script(string endpointName, string expectedScript)
    {
        var request = endpointName switch
        {
            nameof(XtreamEndpoint.PlayerApi) => XtreamRequest.Account(),
            nameof(XtreamEndpoint.PanelApi) => XtreamRequest.Panel(),
            nameof(XtreamEndpoint.Xmltv) => XtreamRequest.Xmltv(),
            _ => XtreamRequest.Playlist(),
        };

        var uri = XtreamUrlBuilder.Build(Credentials, request);

        Assert.Contains(expectedScript, uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public void The_format_json_parameter_is_set_only_on_json_calls()
    {
        // xmltv.php and get.php do not return JSON: asking for that format
        // confuses some panels.
        Assert.Contains("format=json", XtreamUrlBuilder.Build(Credentials, XtreamRequest.Account()).ToString(), StringComparison.Ordinal);
        Assert.Contains("format=json", XtreamUrlBuilder.Build(Credentials, XtreamRequest.Panel()).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("format=json", XtreamUrlBuilder.Build(Credentials, XtreamRequest.Xmltv()).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("format=json", XtreamUrlBuilder.Build(Credentials, XtreamRequest.Playlist()).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_playlist_carries_its_type_and_its_container()
    {
        var uri = XtreamUrlBuilder.Build(Credentials, XtreamRequest.Playlist());

        Assert.Contains("type=m3u_plus", uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("output=ts", uri.ToString(), StringComparison.Ordinal);
    }
}
