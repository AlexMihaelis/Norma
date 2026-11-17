using Norma.Core.Energy;

namespace Norma.Api.Features.Profiles;

/// <summary>Профиль пользователя в ответе API.</summary>
public sealed record ProfileResponse(
    Sex? Sex,
    DateOnly? BirthDate,
    double? HeightCm,
    ActivityLevel? Activity,
    Goal? Goal,
    double? KgPerWeek,
    DateTimeOffset UpdatedAt);

/// <summary>Полная замена профиля: незаполненные поля стираются.</summary>
public sealed record UpdateProfileRequest(
    Sex? Sex,
    DateOnly? BirthDate,
    double? HeightCm,
    ActivityLevel? Activity,
    Goal? Goal,
    double? KgPerWeek);
