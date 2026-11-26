using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Norma.Api.Tests.Account;

/// <summary>
/// Интеграционные тесты аккаунта: ник в шапке, аватар картинкой и откат к
/// инициалам, когда картинки нет.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountTests
{
    // 1x1 PNG: минимальная валидная картинка, чтобы проверить путь без больших файлов.
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly PostgresFixture database;

    public AccountTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task New_user_has_no_nickname_and_no_avatar()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "account-new@example.com");

        var me = await GetMeAsync(app, user.Token);

        Assert.Equal(user.Email, me.Email);
        Assert.Null(me.Nickname);
        Assert.Null(me.AvatarDataUrl);
    }

    [Fact]
    public async Task Nickname_is_saved_and_returned()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "account-nick@example.com");

        using var response = await SendAsync(
            app, HttpMethod.Put, "/api/v1/me/nickname", user.Token, new { nickname = "Аня" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<MePayload>();
        Assert.Equal("Аня", updated!.Nickname);

        var me = await GetMeAsync(app, user.Token);
        Assert.Equal("Аня", me.Nickname);
    }

    [Fact]
    public async Task Nickname_can_be_cleared()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "account-clear@example.com");

        await SendAsync(app, HttpMethod.Put, "/api/v1/me/nickname", user.Token, new { nickname = "Аня" });
        await SendAsync(app, HttpMethod.Put, "/api/v1/me/nickname", user.Token, new { nickname = "" });

        var me = await GetMeAsync(app, user.Token);
        Assert.Null(me.Nickname);
    }

    [Fact]
    public async Task Nickname_validation_rejects_too_short_and_too_long()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "account-nick-invalid@example.com");

        using var tooShort = await SendAsync(
            app, HttpMethod.Put, "/api/v1/me/nickname", user.Token, new { nickname = "А" });

        using var tooLong = await SendAsync(
            app, HttpMethod.Put, "/api/v1/me/nickname", user.Token, new { nickname = new string('я', 31) });

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Avatar_is_stored_and_returned_as_data_url()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "account-avatar@example.com");

        using var response = await SendAsync(
            app,
            HttpMethod.Put,
            "/api/v1/me/avatar",
            user.Token,
            new { dataUrl = $"data:image/png;base64,{OnePixelPng}" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await GetMeAsync(app, user.Token);

        Assert.NotNull(me.AvatarDataUrl);
        Assert.StartsWith("data:image/png;base64,", me.AvatarDataUrl);
    }

    [Fact]
    public async Task Avatar_rejects_unsupported_type()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "account-avatar-invalid@example.com");

        using var notImage = await SendAsync(
            app,
            HttpMethod.Put,
            "/api/v1/me/avatar",
            user.Token,
            new { dataUrl = "data:text/plain;base64,aGVsbG8=" });

        using var broken = await SendAsync(
            app,
            HttpMethod.Put,
            "/api/v1/me/avatar",
            user.Token,
            new { dataUrl = "data:image/png;base64,не-base64" });

        Assert.Equal(HttpStatusCode.BadRequest, notImage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
    }

    [Fact]
    public async Task Avatar_can_be_deleted()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "account-avatar-delete@example.com");

        await SendAsync(
            app,
            HttpMethod.Put,
            "/api/v1/me/avatar",
            user.Token,
            new { dataUrl = $"data:image/png;base64,{OnePixelPng}" });

        using var deleted = await SendAsync(app, HttpMethod.Delete, "/api/v1/me/avatar", user.Token);

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var me = await GetMeAsync(app, user.Token);
        Assert.Null(me.AvatarDataUrl);
    }

    [Fact]
    public async Task Account_endpoints_require_token()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        using var nickname = await app.Client.PutAsJsonAsync("/api/v1/me/nickname", new { nickname = "Аня" });
        using var avatar = await app.Client.PutAsJsonAsync(
            "/api/v1/me/avatar",
            new { dataUrl = $"data:image/png;base64,{OnePixelPng}" });
        using var deleted = await app.Client.DeleteAsync("/api/v1/me/avatar");

        Assert.Equal(HttpStatusCode.Unauthorized, nickname.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, avatar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleted.StatusCode);
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

    private static async Task<MePayload> GetMeAsync(ApiApp app, string token)
    {
        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/me", token);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MePayload>();

        Assert.NotNull(payload);

        return payload;
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
    private async Task<string> ResetAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record MePayload(
        Guid UserId,
        string Email,
        string? Nickname,
        string? AvatarDataUrl);
}