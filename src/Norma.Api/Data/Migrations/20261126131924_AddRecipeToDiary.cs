using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeToDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "FoodId",
                table: "meal_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId",
                table: "meal_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_items_RecipeId",
                table: "meal_items",
                column: "RecipeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_items_food_or_recipe",
                table: "meal_items",
                sql: "(\"FoodId\" IS NULL) <> (\"RecipeId\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_meal_items_recipes_RecipeId",
                table: "meal_items",
                column: "RecipeId",
                principalTable: "recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_meal_items_recipes_RecipeId",
                table: "meal_items");

            migrationBuilder.DropIndex(
                name: "IX_meal_items_RecipeId",
                table: "meal_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_items_food_or_recipe",
                table: "meal_items");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "meal_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "FoodId",
                table: "meal_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
