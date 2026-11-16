using Norma.Api.Features.Health;
using Norma.Api.Features.Plan;

var app = ApiHost.Build(args);

app.Run();

/// <summary>
/// Сборка приложения отдельным методом.
/// </summary>
/// <remarks>
/// Нужна интеграционным тестам: они поднимают реальный Kestrel на свободном порту и
/// общаются по HTTP, не требуя пакета <c>Microsoft.AspNetCore.Mvc.Testing</c>.
/// </remarks>
public static class ApiHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddProblemDetails();

        var app = builder.Build();

        app.UseExceptionHandler();

        // Версия API фиксируется в пути с первого дня: мобильный клиент появится
        // позже и не должен ломать веб-версию при изменении контрактов.
        var api = app.MapGroup("/api/v1");
        api.MapHealth();
        api.MapPlan();

        return app;
    }
}

/// <summary>
/// Делает неявный класс точки входа видимым снаружи — понадобится, если позже
/// перейдём на <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
