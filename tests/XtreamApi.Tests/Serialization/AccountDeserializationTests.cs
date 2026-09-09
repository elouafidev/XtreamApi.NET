using System.Text.Json;
using XtreamApi.Models;
using XtreamApi.Serialization;

namespace XtreamApi.Tests.Serialization;

public class AccountDeserializationTests
{
    private static XtreamAccount Read(string json)
    {
        var account = JsonSerializer.Deserialize<XtreamAccount>(json, XtreamJson.Default);
        Assert.NotNull(account);
        return account;
    }

    private static UserInfo ReadUser(string json)
    {
        var user = Read(json).User;
        Assert.NotNull(user);
        return user;
    }

    private static ServerInfo ReadServer(string json)
    {
        var server = Read(json).Server;
        Assert.NotNull(server);
        return server;
    }

    [Fact]
    public void Reads_an_account_whose_every_number_is_returned_as_a_string()
    {
        const string json = """
        {
          "user_info": {
            "username": "demo",
            "password": "secret",
            "auth": 1,
            "status": "Active",
            "exp_date": "1798761600",
            "is_trial": "0",
            "active_cons": "2",
            "created_at": "1700000000",
            "max_connections": "3",
            "allowed_output_formats": ["m3u8", "ts", "rtmp"]
          },
          "server_info": {
            "url": "panel.example.com",
            "port": "8080",
            "https_port": "8443",
            "server_protocol": "http",
            "rtmp_port": "1935",
            "timezone": "Europe/Paris",
            "timestamp_now": 1767225600,
            "time_now": "2026-01-01 00:00:00"
          }
        }
        """;

        var account = Read(json);
        var user = account.User;

        Assert.NotNull(user);
        Assert.Equal("demo", user.Username);
        Assert.True(user.IsAuthenticated);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal(2, user.ActiveConnections);
        Assert.Equal(3, user.MaxConnections);
        Assert.Equal(1, user.AvailableConnections);
        Assert.Equal(["m3u8", "ts", "rtmp"], user.AllowedOutputFormats);
        Assert.Equal(1935, account.Server?.RtmpPort);
        Assert.True(account.IsUsable);
    }

    [Fact]
    public void An_authenticated_but_expired_account_is_not_usable()
    {
        // A real trap: the panel answers auth = 1 although the subscription is
        // over. Testing auth alone leaves the application believing it connected.
        const string json = """
        {"user_info":{"auth":1,"status":"Expired","exp_date":"1600000000"}}
        """;

        var user = ReadUser(json);

        Assert.True(user.IsAuthenticated);
        Assert.True(user.IsExpired);
        Assert.False(user.IsUsable);
    }

    [Fact]
    public void IsTrial_reads_is_trial_and_not_the_connection_count()
    {
        // Regression guard: reading active_cons to answer "is this a trial?" is a
        // mistake other clients have made.
        const string json = """
        {"user_info":{"auth":1,"status":"Active","is_trial":"1","active_cons":"0"}}
        """;

        var user = ReadUser(json);

        Assert.True(user.IsTrial);
        Assert.Equal(0, user.ActiveConnections);
        Assert.True(user.IsUsable);
    }

    [Fact]
    public void An_active_account_with_no_open_connection_stays_usable()
    {
        // Same family of mistake: deriving the account state from the connection
        // count marks as inactive every subscriber who simply is not watching.
        const string json = """{"user_info":{"auth":1,"status":"Active","active_cons":"0"}}""";

        Assert.True(ReadUser(json).IsUsable);
    }

    [Fact]
    public void A_null_expiry_date_means_a_subscription_with_no_end()
    {
        const string json = """
        {"user_info":{"auth":1,"status":"Active","exp_date":null,"max_connections":"0"}}
        """;

        var user = ReadUser(json);

        Assert.Null(user.ExpiresAt);
        Assert.True(user.IsUnlimited);
        Assert.False(user.IsExpired);
        Assert.Null(user.TimeRemaining);
        Assert.Null(user.AvailableConnections);
    }

    [Fact]
    public void A_failed_sign_in_is_read_without_an_exception()
    {
        const string json = """{"user_info":{"auth":0}}""";

        var user = ReadUser(json);

        Assert.False(user.IsAuthenticated);
        Assert.Equal(AccountStatus.Unknown, user.Status);
        Assert.False(user.IsUsable);
    }

    [Theory]
    // Over http it is "port" that counts.
    [InlineData("http", "8080", "8443", "http://panel.example.com:8080/")]
    // Over https it is "https_port": taking "port" would produce a dead URL.
    [InlineData("https", "8080", "8443", "https://panel.example.com:8443/")]
    public void The_base_address_pairs_the_right_port_with_the_right_scheme(
        string protocol,
        string port,
        string httpsPort,
        string expected)
    {
        var json = $$$"""
        {"server_info":{"url":"panel.example.com","port":"{{{port}}}","https_port":"{{{httpsPort}}}","server_protocol":"{{{protocol}}}"}}
        """;

        Assert.Equal(expected, ReadServer(json).BaseAddress?.ToString());
    }

    [Fact]
    public void The_base_address_tolerates_an_already_complete_url_field()
    {
        const string json = """
        {"server_info":{"url":"http://panel.example.com:1234","port":"8080","server_protocol":"http"}}
        """;

        // The port from the dedicated field wins over the one glued to the host.
        Assert.Equal("http://panel.example.com:8080/", ReadServer(json).BaseAddress?.ToString());
    }

    [Fact]
    public void The_base_address_is_undetermined_without_a_host()
    {
        const string json = """{"server_info":{"url":"","port":"8080"}}""";

        Assert.Null(ReadServer(json).BaseAddress);
    }

    [Fact]
    public void The_non_iso_server_time_is_read()
    {
        // "2026-01-01 00:00:00" is not ISO 8601: the standard System.Text.Json
        // reader rejects this form.
        const string json = """
        {"server_info":{"time_now":"2026-01-01 00:00:00","timestamp_now":1767225600}}
        """;

        var server = ReadServer(json);
        var expected = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, server.TimeNow);
        Assert.Equal(expected, server.TimestampNow);
    }
}
