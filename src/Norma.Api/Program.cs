using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;
using Norma.Api.Features.Auth.Login;
using Norma.Api.Features.Auth.Me;
using Norma.Api.Features.Auth.Register;
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

        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();

        // Версия API фиксируется в пути с первого дня: мобильный клиент появится
        // позже и не должен ломать веб-версию при изменении контрактов.
        var api = app.MapGroup("/api/v1");
        api.MapHealth();
        api.MapPlan();
        api.MapSearchFoods();
        api.MapRegister();
        api.MapLogin();
        api.MapMe();

        return app;
    }
}

/// <summary>
/// Делает неявный класс точки входа видимым снаружи — понадобится, если позже
/// перейдём на <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
