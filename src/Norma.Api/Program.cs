using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Foods.SearchFoods;
using Norma.Api.Features.Health;
using Norma.Api.Features.Plan;

var app = ApiHost.Build(args);

app.Run();

/// <summary>
/// Сборка приложения отдельным методом.
/// </summary>
/// <remarks>
/// Нужна интеграционным тестам: они поднимают реальный Kestrel на свободном порту и
/// общаются по HTTP. Параметр <c>configure</c> позволяет тесту подменить конфигурацию
/// (например, строку подключения к контейнеру Postgres) до регистрации сервисов.
/// </remarks>
public static class ApiHost
{
    // Подстраховка для тестов: appsettings.json не копируется в выход тестового
    // проекта, а строка подключения нужна уже при регистрации DbContext. Живая
    // конфигурация всегда приходит из appsettings.json.
    private const string DevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=norma;Username=norma;Password=norma_dev_password";

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        configure?.Invoke(builder);

        builder.Services.AddProblemDetails();

        // Enum в JSON строками: фронтенду проще читать "Cooked", чем 2.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var connectionString = builder.Configuration.GetConnectionString("Norma")
            ?? DevelopmentConnectionString;

        builder.Services.AddDbContext<NormaDbContext>(options => options.UseNpgsql(connectionString));

        var app = builder.Build();

        app.UseExceptionHandler();

        // Версия API фиксируется в пути с первого дня: мобильный клиент появится
        // позже и не должен ломать веб-версию при изменении контрактов.
        var api = app.MapGroup("/api/v1");
        api.MapHealth();
        api.MapPlan();
        api.MapSearchFoods();

        return app;
    }
}

/// <summary>
/// Делает неявный класс точки входа видимым снаружи — понадобится, если позже
/// перейдём на <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
