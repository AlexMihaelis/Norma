using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeTagsAndPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "recipes");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "recipes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoBytes",
                table: "recipes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "recipes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recipe_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_tags_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipes_UserId_IsFavorite",
                table: "recipes",
                columns: new[] { "UserId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_tags_RecipeId_Tag",
                table: "recipe_tags",
                columns: new[] { "RecipeId", "Tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipe_tags");

            migrationBuilder.DropIndex(
                name: "IX_recipes_UserId_IsFavorite",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "PhotoBytes",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "recipes");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "recipes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
