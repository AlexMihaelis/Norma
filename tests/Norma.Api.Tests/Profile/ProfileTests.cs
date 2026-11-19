using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;

namespace Norma.Api.Tests.Profile;

/// <summary>Профиль: сохранение, изоляция между пользователями, валидация.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ProfileTests
{
    private readonly PostgresFixture database;

    public ProfileTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Profile_is_saved_and_returned()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "profile@example.com");

        using var put = await SendAsync(app, HttpMethod.Put, "/api/v1/me/profile", user.Token, new
        {
            sex = "Female",
            birthDate = "2001-05-10",
            heightCm = 175.0,
            activity = "Sedentary",
            goal = "Lose",
            targetWeightKg = 62.5,
            kgPerWeek = 0.25,
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var get = await SendAsync(app, HttpMethod.Get, "/api/v1/me/profile", user.Token);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var profile = await get.Content.ReadFromJsonAsync<ProfilePayload>();

        Assert.NotNull(profile);
        Assert.Equal("Female", profile.Sex);
        Assert.Equal(new DateOnly(2001, 5, 10), profile.BirthDate);
        Assert.Equal(175.0, profile.HeightCm);
        Assert.Equal("Sedentary", profile.Activity);
        Assert.Equal("Lose", profile.Goal);
        Assert.Equal(62.5, profile.TargetWeightKg);
        Assert.Equal(0.25, profile.KgPerWeek);
    }

    [Fact]
    public async Task Profile_is_isolated_between_users()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-profile@example.com");
        var bob = await RegisterAsync(app, "bob-profile@example.com");

        using var put = await SendAsync(app, HttpMethod.Put, "/api/v1/me/profile", alice.Token, new { sex = "Female" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var bobGet = await SendAsync(app, HttpMethod.Get, "/api/v1/me/profile", bob.Token);

        Assert.Equal(HttpStatusCode.NotFound, bobGet.StatusCode);
    }

    [Fact]
    public async Task Get_without_profile_returns_not_found()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "noprofile@example.com");

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/me/profile", user.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_requires_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.PutAsJsonAsync("/api/v1/me/profile", new { sex = "Male" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_rejects_invalid_height()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "invalid-profile@example.com");

        using var response = await SendAsync(app, HttpMethod.Put, "/api/v1/me/profile", user.Token, new { heightCm = 50.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_rejects_invalid_target_weight()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "invalid-target@example.com");

        using var response = await SendAsync(
            app, HttpMethod.Put, "/api/v1/me/profile", user.Token, new { targetWeightKg = 10.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_replaces_profile_completely()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "replace@example.com");

        using var first = await SendAsync(app, HttpMethod.Put, "/api/v1/me/profile", user.Token, new
        {
            sex = "Male",
            heightCm = 181.0,
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // PUT - полная замена: поле, которого нет в запросе, стирается.
        using var second = await SendAsync(app, HttpMethod.Put, "/api/v1/me/profile", user.Token, new { heightCm = 182.0 });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var get = await SendAsync(app, HttpMethod.Get, "/api/v1/me/profile", user.Token);
        var profile = await get.Content.ReadFromJsonAsync<ProfilePayload>();

        Assert.NotNull(profile);
        Assert.Null(profile.Sex);
        Assert.Equal(182.0, profile.HeightCm);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        ApiApp app, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await app.Client.SendAsync(request);
    }

    private static async Task<AuthPayload> RegisterAsync(ApiApp app, string email)
    {
        using var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "correct-horse-battery" });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthPayload>();

        Assert.NotNull(payload);

        return payload;
    }

    private static Task<ApiApp> StartApiAsync(string connectionString) =>
        ApiApp.StartAsync(builder => builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Norma"] = connectionString,
            }));

    /// <summary>Чистые таблицы и строка подключения: тест не зависит от предыдущего.</summary>
    private async Task<string> MigrateAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record ProfilePayload(
        string? Sex,
        DateOnly? BirthDate,
        double? HeightCm,
        string? Activity,
        string? Goal,
        double? TargetWeightKg,
        double? KgPerWeek);
}
