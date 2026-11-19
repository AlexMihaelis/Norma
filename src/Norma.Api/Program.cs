using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Activity.AddActivity;
using Norma.Api.Features.Activity.GetActivityDay;
using Norma.Api.Features.Auth;
using Norma.Api.Features.Auth.Login;
using Norma.Api.Features.Auth.Me;
using Norma.Api.Features.Auth.Register;
using Norma.Api.Features.Diary.AddDiaryEntry;
using Norma.Api.Features.Diary.DeleteDiaryEntry;
using Norma.Api.Features.Diary.GetDiaryDay;
using Norma.Api.Features.Diary.RepeatDiaryDay;
using Norma.Api.Features.Favorites.AddFavorite;
using Norma.Api.Features.Favorites.DeleteFavorite;
using Norma.Api.Features.Favorites.ListFavorites;
using Norma.Api.Features.Foods.CreateFood;
using Norma.Api.Features.Foods.SearchFoods;
using Norma.Api.Features.Health;
using Norma.Api.Features.Plan;
using Norma.Api.Features.Profiles.GetProfile;
using Norma.Api.Features.Profiles.UpdateProfile;
using Norma.Api.Features.Stats.GetStats;
using Norma.Api.Features.Water.AddWater;
using Norma.Api.Features.Water.GetWaterDay;
using Norma.Api.Features.Weight.AddWeight;
using Norma.Api.Features.Weight.GetWeightTrend;
using Norma.Api.Features.Weight.ListWeight;
using Scalar.AspNetCore;

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

    // Ключ подписи для локальной разработки и тестов. В продакшене обязан
    // приходить из конфигурации окружения: с этим ключом токены подделываемы.
    private const string DevelopmentJwtKey = "norma-dev-only-signing-key-0123456789abcdef";

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        configure?.Invoke(builder);

        builder.Services.AddProblemDetails();

        // Enum в JSON строками: фронтенду проще читать "Cooked", чем 2.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Документ строится из маршрутов; XML-описания включаются через csproj.
        builder.Services.AddOpenApi(options =>
        {
            // Схема Bearer: в Scalar появится поле для токена.
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Norma API";
                document.Info.Version = "v1";
                document.Info.Description = "Дневник питания, веса и активности.";

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT из /api/v1/auth/login или /api/v1/auth/register.",
                };

                return Task.CompletedTask;
            });
        });

        var connectionString = builder.Configuration.GetConnectionString("Norma")
            ?? DevelopmentConnectionString;

        builder.Services.AddDbContext<NormaDbContext>(options => options.UseNpgsql(connectionString));

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Key))
        {
            jwtOptions = jwtOptions with { Key = DevelopmentJwtKey };
        }

        if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
        {
            throw new InvalidOperationException("Jwt:Key должен быть не короче 32 байт.");
        }

        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<JwtTokenIssuer>();
        builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Не переименовывать claim'ы: читаем ровно "sub" и "email".
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                };
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        // Удобство разработки: при запуске схема догоняет миграции, чтобы после
        // git pull не вспоминать про отдельную команду. Включено только в
        // Development и только флагом из appsettings.Development.json: в тестах
        // схему готовят Testcontainers, в продакшене миграции - шаг деплоя, а не
        // побочный эффект старта приложения (там инстансов несколько).
        if (app.Environment.IsDevelopment()
            && app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            try
            {
                using var scope = app.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<NormaDbContext>().Database.Migrate();
                app.Logger.LogInformation("Схема базы синхронизирована с миграциями.");
            }
            catch (Exception exception)
            {
                // База может быть не поднята. Падать на старте из-за этого хуже,
                // чем подсказать, что именно сделать: API поднимается, а в логе
                // видно причину.
                app.Logger.LogError(
                    exception,
                    "Схема базы не обновлена. Подними базу (docker compose up -d db) и перезапусти API.");
            }
        }

        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();

        // Документ: /openapi/v1.json, UI: /scalar/v1; маршруты анонимные, данных в них нет.
        app.MapOpenApi();
        app.MapScalarApiReference();

        // Версия API фиксируется в пути с первого дня: мобильный клиент появится
        // позже и не должен ломать веб-версию при изменении контрактов.
        var api = app.MapGroup("/api/v1");
        api.MapHealth();
        api.MapPlan();
        api.MapSearchFoods();
        api.MapCreateFood();
        api.MapRegister();
        api.MapLogin();
        api.MapMe();
        api.MapAddWeight();
        api.MapListWeight();
        api.MapGetWeightTrend();
        api.MapGetProfile();
        api.MapUpdateProfile();
        api.MapAddWater();
        api.MapGetWaterDay();
        api.MapAddActivity();
        api.MapGetActivityDay();
        api.MapAddDiaryEntry();
        api.MapGetDiaryDay();
        api.MapDeleteDiaryEntry();
        api.MapRepeatDiaryDay();
        api.MapAddFavorite();
        api.MapListFavorites();
        api.MapDeleteFavorite();
        api.MapGetStats();

        return app;
    }
}

/// <summary>
/// Делает неявный класс точки входа видимым снаружи — понадобится, если позже
/// перейдём на <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
