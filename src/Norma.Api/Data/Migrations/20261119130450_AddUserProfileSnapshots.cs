using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_profile_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Sex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HeightCm = table.Column<double>(type: "double precision", nullable: true),
                    Activity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Goal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TargetWeightKg = table.Column<double>(type: "double precision", nullable: true),
                    KgPerWeek = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profile_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_profile_snapshots_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_snapshots_UserId_EffectiveFrom",
                table: "user_profile_snapshots",
                columns: new[] { "UserId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_profile_snapshots");
        }
    }
}
