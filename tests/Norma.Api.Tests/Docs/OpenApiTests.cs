using System.Net;

namespace Norma.Api.Tests.Docs;

/// <summary>
/// Документация API: документ OpenAPI отдаётся и перечисляет боевые маршруты,
/// UI Scalar открывается. Тесты без БД: генерация документа к базе не ходит.
/// </summary>
public sealed class OpenApiTests
{
    [Fact]
    public async Task OpenApi_document_lists_public_endpoints()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("/api/v1/plan", json);
        Assert.Contains("/api/v1/foods/search", json);
        Assert.Contains("/api/v1/auth/login", json);
        Assert.Contains("/api/v1/me", json);
    }

    [Fact]
    public async Task OpenApi_document_describes_bearer_auth()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();

        // Схема нужна, чтобы в Scalar появилось поле для токена.
        Assert.Contains("securitySchemes", json, StringComparison.Ordinal);
        Assert.Contains("Bearer", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scalar_ui_is_served()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
