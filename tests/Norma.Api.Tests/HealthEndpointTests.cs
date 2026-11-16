using System.Net;
using System.Net.Http.Json;

namespace Norma.Api.Tests;

/// <summary>
/// Дымовой тест: поднимает реальный Kestrel в процессе теста и проверяет, что хост
/// и версия API живы. Первая линия обороны — если он падает, сломан не расчёт, а
/// само приложение.
/// </summary>
public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_returns_ok_and_version()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();

        Assert.NotNull(payload);
        Assert.Equal("ok", payload.Status);
        Assert.False(string.IsNullOrWhiteSpace(payload.Version));
    }

    private sealed record HealthPayload(string Status, string Version);
}
