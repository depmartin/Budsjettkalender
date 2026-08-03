using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Internkalender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GjoeremaalRegler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tittel = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Notat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tidfestingstype = table.Column<int>(type: "int", nullable: false),
                    Maaned = table.Column<int>(type: "int", nullable: true),
                    Dag = table.Column<int>(type: "int", nullable: true),
                    AarforskyvningJustering = table.Column<int>(type: "int", nullable: false),
                    Datokvalifikator = table.Column<int>(type: "int", nullable: true),
                    AnkerLoep = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AnkerOffsetDager = table.Column<int>(type: "int", nullable: false),
                    Rundeposisjon = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GjoeremaalRegler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InternRunder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rundetype = table.Column<int>(type: "int", nullable: false),
                    Aar = table.Column<int>(type: "int", nullable: true),
                    OpprettetTid = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpprettetAv = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SistSynkronisert = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternRunder", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegelAnsvarlige",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrukerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Navn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegelAnsvarlige", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegelAnsvarlige_GjoeremaalRegler_RegelId",
                        column: x => x.RegelId,
                        principalTable: "GjoeremaalRegler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegelRundetyper",
                columns: table => new
                {
                    RegelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rundetype = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegelRundetyper", x => new { x.RegelId, x.Rundetype });
                    table.ForeignKey(
                        name: "FK_RegelRundetyper_GjoeremaalRegler_RegelId",
                        column: x => x.RegelId,
                        principalTable: "GjoeremaalRegler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterneGjoeremaal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RundeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tittel = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Notat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tidfestingstype = table.Column<int>(type: "int", nullable: false),
                    Dato = table.Column<DateOnly>(type: "date", nullable: true),
                    Datopresisjon = table.Column<int>(type: "int", nullable: false),
                    Datokvalifikator = table.Column<int>(type: "int", nullable: true),
                    AnkerLoep = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AnkerOffsetDager = table.Column<int>(type: "int", nullable: false),
                    Rundeposisjon = table.Column<int>(type: "int", nullable: true),
                    Sorteringsdag = table.Column<DateOnly>(type: "date", nullable: true),
                    VenterPaaAnker = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FullfoertAvId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FullfoertAvNavn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FullfoertTid = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Opphav = table.Column<int>(type: "int", nullable: false),
                    GenerertFraRegelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManueltEndret = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterneGjoeremaal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterneGjoeremaal_InternRunder_RundeId",
                        column: x => x.RundeId,
                        principalTable: "InternRunder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GjoeremaalAnsvarlige",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GjoeremaalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrukerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Navn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GjoeremaalAnsvarlige", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GjoeremaalAnsvarlige_InterneGjoeremaal_GjoeremaalId",
                        column: x => x.GjoeremaalId,
                        principalTable: "InterneGjoeremaal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GjoeremaalAnsvarlige_BrukerId",
                table: "GjoeremaalAnsvarlige",
                column: "BrukerId");

            migrationBuilder.CreateIndex(
                name: "IX_GjoeremaalAnsvarlige_GjoeremaalId",
                table: "GjoeremaalAnsvarlige",
                column: "GjoeremaalId");

            migrationBuilder.CreateIndex(
                name: "IX_InterneGjoeremaal_RundeId",
                table: "InterneGjoeremaal",
                column: "RundeId");

            migrationBuilder.CreateIndex(
                name: "IX_InterneGjoeremaal_Sorteringsdag",
                table: "InterneGjoeremaal",
                column: "Sorteringsdag");

            migrationBuilder.CreateIndex(
                name: "IX_InternRunder_Rundetype_Aar",
                table: "InternRunder",
                columns: new[] { "Rundetype", "Aar" });

            migrationBuilder.CreateIndex(
                name: "IX_RegelAnsvarlige_RegelId",
                table: "RegelAnsvarlige",
                column: "RegelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GjoeremaalAnsvarlige");

            migrationBuilder.DropTable(
                name: "RegelAnsvarlige");

            migrationBuilder.DropTable(
                name: "RegelRundetyper");

            migrationBuilder.DropTable(
                name: "InterneGjoeremaal");

            migrationBuilder.DropTable(
                name: "GjoeremaalRegler");

            migrationBuilder.DropTable(
                name: "InternRunder");
        }
    }
}
