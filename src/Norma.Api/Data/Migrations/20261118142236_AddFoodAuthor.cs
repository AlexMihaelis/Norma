using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "foods",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_foods_CreatedByUserId",
                table: "foods",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_foods_users_CreatedByUserId",
                table: "foods",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_foods_users_CreatedByUserId",
                table: "foods");

            migrationBuilder.DropIndex(
                name: "IX_foods_CreatedByUserId",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "foods");
        }
    }
}
