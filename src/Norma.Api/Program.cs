using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Data.Seeding;
using Norma.Api.Features.Account.DeleteAvatar;
using Norma.Api.Features.Account.UpdateAvatar;
using Norma.Api.Features.Account.UpdateNickname;
using Norma.Api.Features.Activity.AddActivity;
using Norma.Api.Features.Activity.GetActivityDay;
using Norma.Api.Features.Auth;
using Norma.Api.Features.Auth.Login;
using Norma.Api.Features.Auth.Me;
using Norma.Api.Features.Auth.Register;
using Norma.Api.Features.BodyMeasurements.AddBodyMeasurement;
using Norma.Api.Features.BodyMeasurements.GetLatestBodyMeasurements;
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
using Norma.Api.Features.Recipes.CreateRecipe;
using Norma.Api.Features.Recipes.DeleteRecipe;
using Norma.Api.Features.Recipes.GetRecipe;
using Norma.Api.Features.Recipes.ListRecipes;
using Norma.Api.Features.Recipes.ListRecipeTags;
using Norma.Api.Features.Recipes.SetRecipeFavorite;
using Norma.Api.Features.Recipes.UpdateRecipe;
using Norma.Api.Features.Stats.GetStats;
using Norma.Api.Features.Water.AddWater;
using Norma.Api.Features.Water.DeleteLastWater;
using Norma.Api.Features.Water.GetWaterDay;
using Norma.Api.Features.Weight.AddWeight;
using Norma.Api.Features.Weight.GetWeightTrend;
using Norma.Api.Features.Weight.ListWeight;
using Norma.Api.Features.Weight.UpdateWeight;
using Scalar.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerUI;

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
            // Схема Bearer и требование ее для операций: кнопка Authorize в
            // Swagger и поле токена в Scalar иначе не подставят заголовок.
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

                // Анонимные ручки (health, вход, регистрация) лишний заголовок
                // игнорируют, поэтому требование безопасно объявить глобально.
                document.SecurityRequirements.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    }] = [],
                });

                return Task.CompletedTask;
            });
        });

        var connectionString = builder.Configuration.GetConnectionString("Norma")
            ?? DevelopmentConnectionString;

        builder.Services.AddDbContext<NormaDbContext>(options => options.UseNpgsql(connectionString));

        // Стартовый набор продуктов: только Development и по флагу, чтобы не лить
        // данные в тестовые и продуктовые базы. Повторный запуск ничего не дублирует.
        if (builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("Database:SeedFoodsOnStartup"))
        {
            builder.Services.AddHostedService<FoodSeedHostedService>();
        }

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

        // Браузер ходит в API с другого адреса (Angular dev server), поэтому нужен
        // CORS. Список источников - из конфигурации: в проде там домен приложения.
        var allowedOrigins = builder.Configuration.GetSection("Web:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200"];

        builder.Services.AddCors(options => options.AddPolicy(
            WebCorsPolicy.Name,
            policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

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
        app.UseCors(WebCorsPolicy.Name);
        app.UseAuthentication();
        app.UseAuthorization();

        // Документ: /openapi/v1.json; UI: /scalar/v1 и /swagger.
        app.MapOpenApi();
        app.MapScalarApiReference();

        // Swagger UI привычнее части команды; смотрит в тот же документ, что и Scalar.
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Norma API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Norma API";
        });

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
        api.MapUpdateNickname();
        api.MapUpdateAvatar();
        api.MapDeleteAvatar();
        api.MapAddWeight();
        api.MapListWeight();
        api.MapUpdateWeight();
        api.MapGetWeightTrend();
        api.MapAddBodyMeasurement();
        api.MapGetLatestBodyMeasurements();
        api.MapGetProfile();
        api.MapUpdateProfile();
        api.MapAddWater();
        api.MapGetWaterDay();
        api.MapDeleteLastWater();
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
        api.MapCreateRecipe();
        api.MapListRecipes();
        api.MapListRecipeTags();
        api.MapGetRecipe();
        api.MapUpdateRecipe();
        api.MapSetRecipeFavorite();
        api.MapDeleteRecipe();

        return app;
    }
}

/// <summary>Имя политики CORS для браузерного клиента.</summary>
public static class WebCorsPolicy
{
    public const string Name = "web";
}

/// <summary>
/// Делает неявный класс точки входа видимым снаружи — понадобится, если позже
/// перейдём на <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
