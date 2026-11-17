using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Testcontainers.PostgreSql;

namespace Norma.Api.Tests.Auth;

/// <summary>
/// Интеграционные тесты аутентификации: регистрация, вход, защищённый эндпоинт.
/// Каждый тест получает свой контейнер Postgres и чистую базу.
/// </summary>
public sealed class AuthTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task Register_returns_token_and_me_returns_user()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var register = await RegisterAsync(app, "miha@example.com", "correct-horse-battery");

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var auth = await register.Content.ReadFromJsonAsync<AuthPayload>();

        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.Equal("miha@example.com", auth.Email);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var me = await app.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var profile = await me.Content.ReadFromJsonAsync<MePayload>();

        Assert.NotNull(profile);
        Assert.Equal(auth.UserId, profile.UserId);
        Assert.Equal("miha@example.com", profile.Email);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var first = await RegisterAsync(app, "Miha@Example.com", "correct-horse-battery");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Регистр не важен: email нормализуется к нижнему регистру.
        using var second = await RegisterAsync(app, "miha@example.com", "another-password");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var register = await RegisterAsync(app, "miha@example.com", "correct-horse-battery");
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "miha@example.com", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_password_returns_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var register = await RegisterAsync(app, "miha@example.com", "correct-horse-battery");
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "miha@example.com", password = "correct-horse-battery" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var auth = await login.Content.ReadFromJsonAsync<AuthPayload>();

        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Fact]
    public async Task Me_without_token_returns_unauthorized()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await RegisterAsync(app, "miha@example.com", "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> RegisterAsync(ApiApp app, string email, string password) =>
        app.Client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });

    private static Task<ApiApp> StartApiAsync(string connectionString) =>
        ApiApp.StartAsync(builder => builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Norma"] = connectionString,
            }));

    private async Task<string> MigrateAsync()
    {
        var connectionString = database.GetConnectionString();
        var options = new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(connectionString).Options;

        await using var db = new NormaDbContext(options);
        await db.Database.MigrateAsync();

        return connectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record MePayload(Guid UserId, string Email);
}
