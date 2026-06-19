using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase2Innhenting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SisteForsoek",
                table: "BehandledeDokumenter",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UttrekksForsoek",
                table: "BehandledeDokumenter",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "InnhentingsStatuser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kilde = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SisteVellykkedeOppdagelse = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SisteVellykkedeHenting = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SisteForsoek = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SisteUtfall = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SisteFeilmelding = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InnhentingsStatuser", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InnhentingsStatuser_Kilde",
                table: "InnhentingsStatuser",
                column: "Kilde",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InnhentingsStatuser");

            migrationBuilder.DropColumn(
                name: "SisteForsoek",
                table: "BehandledeDokumenter");

            migrationBuilder.DropColumn(
                name: "UttrekksForsoek",
                table: "BehandledeDokumenter");
        }
    }
}
