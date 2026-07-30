using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Kalenderabonnement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kalenderabonnementer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GruppeKode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Etikett = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OpprettetAv = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OpprettetTid = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aktiv = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kalenderabonnementer", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kalenderabonnementer_Token",
                table: "Kalenderabonnementer",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Kalenderabonnementer");
        }
    }
}
