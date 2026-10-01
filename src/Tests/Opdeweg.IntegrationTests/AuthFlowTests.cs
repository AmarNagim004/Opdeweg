using System.Net;
using System.Net.Http.Json;
using Opdeweg.Application.DTOs;

namespace Opdeweg.IntegrationTests;

public sealed class AuthFlowTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_refresh_rotation_and_reuse_detection()
    {
        factory.SkipIfUnavailable();
        var driver = await factory.RegisterAsync("Sam");

        var me = await driver.Http.GetFromJsonAsync<MeDto>("/api/v1/me", ApiFactory.Json);
        Assert.Equal("Sam", me!.DisplayName);

        var anonymous = factory.CreateClient();
        var refreshed = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = driver.Auth.RefreshToken });
        refreshed.EnsureSuccessStatusCode();
        var rotated = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>(ApiFactory.Json))!;
        Assert.NotEqual(driver.Auth.RefreshToken, rotated.RefreshToken);

        // An immediate retry with the just-rotated token (lost response, two app contexts) is a benign race.
        var retry = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = driver.Auth.RefreshToken });
        retry.EnsureSuccessStatusCode();

        // Replaying it after the grace window is treated as theft: it fails and revokes the whole family.
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        var replay = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = driver.Auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var afterReuse = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_wrong_password_and_duplicate_emails()
    {
        factory.SkipIfUnavailable();
        var driver = await factory.RegisterAsync("Alex");
        var client = factory.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = driver.Email, password = "not the password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Contains("invalid_credentials", await wrong.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = driver.Email.ToUpperInvariant(), password = "correct horse battery" });
        ok.EnsureSuccessStatusCode();

        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = driver.Email, password = "correct horse battery", displayName = "Alex" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_authentication()
    {
        factory.SkipIfUnavailable();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/driving/sessions", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/config")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
