using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Norma.Api.Tests;

/// <summary>
/// Поднимает реальный Kestrel в процессе теста на свободном порту и отдаёт
/// <see cref="HttpClient"/>. Общая обвязка для интеграционных тестов: каждый тест
/// получает свой экземпляр приложения и сам его гасит.
/// </summary>
public sealed class ApiApp : IAsyncDisposable
{
    private readonly WebApplication app;

    private ApiApp(WebApplication app, HttpClient client)
    {
        this.app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    /// <param name="configure">
    /// Подмена конфигурации до регистрации сервисов: так тесты с Testcontainers
    /// подставляют строку подключения к своему контейнеру Postgres.
    /// </param>
    public static async Task<ApiApp> StartAsync(Action<WebApplicationBuilder>? configure = null)
    {
        var app = ApiHost.Build(["--urls", "http://127.0.0.1:0"], builder =>
        {
            // Тесты не зависят от окружения машины: иначе при Development
            // приложение на старте полезло бы применять миграции к локальной
            // базе разработчика. Свою схему каждый тест готовит сам.
            builder.Environment.EnvironmentName = "Testing";
            configure?.Invoke(builder);
        });

        await app.StartAsync();

        // Порт назначает ОС, поэтому адрес читаем у самого сервера, а не из
        // конфигурации.
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses
            .Single();

        return new ApiApp(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
