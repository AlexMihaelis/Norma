namespace Norma.Api.Features.Health;

/// <summary>
/// Срез «здоровье сервиса».
/// </summary>
/// <remarks>
/// Это первый и самый маленький вертикальный срез: он показывает принятую
/// структуру — один сценарий = одна папка, где рядом лежат контракт ответа и его
/// регистрация. Бизнес-логики здесь нет и не должно быть.
/// </remarks>
public static class HealthEndpoint
{
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/health", () => new HealthResponse(
                Status: "ok",
                Version: typeof(HealthEndpoint).Assembly.GetName().Version?.ToString() ?? "unknown"))
            .WithName("Health");

        return endpoints;
    }
}

/// <summary>Ответ проверки работоспособности.</summary>
/// <param name="Status">Всегда <c>ok</c>: если ответ пришёл, сервис жив.</param>
/// <param name="Version">Версия сборки API, удобна при проверке выкатки.</param>
public sealed record HealthResponse(string Status, string Version);
