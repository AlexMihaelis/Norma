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
    public async Task OpenApi_document_requires_bearer_auth()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync("/openapi/v1.json");
        var json = await response.Content.ReadAsStringAsync();

        // Требование, а не только схема: иначе Authorize в Swagger UI не
        // подставляет токен, а Scalar не показывает поле авторизации.
        Assert.Contains("securitySchemes", json, StringComparison.Ordinal);
        Assert.Contains("Bearer", json, StringComparison.Ordinal);
        Assert.Contains("\"security\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scalar_ui_is_served()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ui_is_served()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("swagger-ui", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
