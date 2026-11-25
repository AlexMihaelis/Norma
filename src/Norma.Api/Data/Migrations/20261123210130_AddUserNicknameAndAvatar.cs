using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNicknameAndAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AvatarBytes",
                table: "users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarContentType",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarBytes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AvatarContentType",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "users");
        }
    }
}
