using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeManualNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ManualCarbsPer100g",
                table: "recipes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ManualFatPer100g",
                table: "recipes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ManualKcalPer100g",
                table: "recipes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ManualProteinPer100g",
                table: "recipes",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualCarbsPer100g",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "ManualFatPer100g",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "ManualKcalPer100g",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "ManualProteinPer100g",
                table: "recipes");
        }
    }
}
