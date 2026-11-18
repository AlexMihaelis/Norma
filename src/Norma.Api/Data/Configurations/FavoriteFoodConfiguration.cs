using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Configurations;

/// <summary>Избранные продукты: уникальны по паре «пользователь и продукт».</summary>
internal sealed class FavoriteFoodConfiguration : IEntityTypeConfiguration<FavoriteFood>
{
    public void Configure(EntityTypeBuilder<FavoriteFood> builder)
    {
        builder.ToTable("favorite_foods");

        builder.HasKey(favorite => favorite.Id);

        // Один продукт в избранном пользователя может быть только один раз:
        // повторное «в избранное» не размножает запись.
        builder.HasIndex(favorite => new { favorite.UserId, favorite.FoodId }).IsUnique();

        builder
            .HasOne(favorite => favorite.User)
            .WithMany()
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Каскад, в отличие от дневника: избранное - закладка, а не факт питания.
        builder
            .HasOne(favorite => favorite.Food)
            .WithMany()
            .HasForeignKey(favorite => favorite.FoodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
