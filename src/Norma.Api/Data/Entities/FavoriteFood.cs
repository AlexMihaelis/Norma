namespace Norma.Api.Data.Entities;

/// <summary>
/// Избранный продукт пользователя. Отметка ставится вручную: попадание в избранное
/// означает, что продукт или блюдо из него готовят часто, а не что его купили.
/// </summary>
/// <remarks>
/// Отдельная таблица, а не флаг у продукта: у каждого пользователя свой список, и
/// править общий каталог ради личной отметки нельзя. Продукт, удаленный из
/// каталога, уносит и отметку (каскад): избранное - закладка, а не запись факта.
/// </remarks>
public sealed class FavoriteFood
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid FoodId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }

    public Food? Food { get; set; }
}
