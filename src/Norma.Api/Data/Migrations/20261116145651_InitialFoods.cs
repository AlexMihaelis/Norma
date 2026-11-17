using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "foods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KcalPer100g = table.Column<double>(type: "double precision", nullable: false),
                    ProteinPer100g = table.Column<double>(type: "double precision", nullable: false),
                    FatPer100g = table.Column<double>(type: "double precision", nullable: false),
                    CarbsPer100g = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_foods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "food_aliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_food_aliases_foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "food_names",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_names", x => x.Id);
                    table.ForeignKey(
                        name: "FK_food_names_foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_food_aliases_FoodId_Alias",
                table: "food_aliases",
                columns: new[] { "FoodId", "Alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_names_FoodId_Locale_Name",
                table: "food_names",
                columns: new[] { "FoodId", "Locale", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_foods_Source_SourceId",
                table: "foods",
                columns: new[] { "Source", "SourceId" },
                unique: true,
                filter: "\"SourceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_aliases");

            migrationBuilder.DropTable(
                name: "food_names");

            migrationBuilder.DropTable(
                name: "foods");
        }
    }
}
