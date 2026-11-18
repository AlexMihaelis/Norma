using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseUnit",
                table: "foods",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Gram");

            migrationBuilder.CreateTable(
                name: "meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meals_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<double>(type: "double precision", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meal_items_foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meal_items_meals_MealId",
                        column: x => x.MealId,
                        principalTable: "meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meal_items_FoodId",
                table: "meal_items",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_meal_items_MealId_CreatedAt",
                table: "meal_items",
                columns: new[] { "MealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_meals_UserId_Date_Kind",
                table: "meals",
                columns: new[] { "UserId", "Date", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_items");

            migrationBuilder.DropTable(
                name: "meals");

            migrationBuilder.DropColumn(
                name: "BaseUnit",
                table: "foods");
        }
    }
}
