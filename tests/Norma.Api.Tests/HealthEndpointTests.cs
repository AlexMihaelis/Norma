using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

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
        var app = ApiHost.Build(["--urls", "http://127.0.0.1:0"]);

        await app.StartAsync();

        try
        {
            // Порт назначает ОС, поэтому адрес читаем у самого сервера, а не из
            // конфигурации.
            var address = app.Services
                .GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses
                .Single();

            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var response = await client.GetAsync("/api/v1/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();

            Assert.NotNull(payload);
            Assert.Equal("ok", payload.Status);
            Assert.False(string.IsNullOrWhiteSpace(payload.Version));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed record HealthPayload(string Status, string Version);
}
