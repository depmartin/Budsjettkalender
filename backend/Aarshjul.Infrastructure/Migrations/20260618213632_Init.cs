using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BehandledeDokumenter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kilde = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DokumentNokkel = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    InnholdHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tittel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForstSett = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BehandletStatus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehandledeDokumenter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Brukere",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Navn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Funksjonsrolle = table.Column<int>(type: "int", nullable: false),
                    ErFin = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brukere", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Forslag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForslagType = table.Column<int>(type: "int", nullable: false),
                    Opphav = table.Column<int>(type: "int", nullable: false),
                    KildeEllerInnsender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tittel = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Dato = table.Column<DateOnly>(type: "date", nullable: true),
                    Datopresisjon = table.Column<int>(type: "int", nullable: false),
                    Datokvalifikator = table.Column<int>(type: "int", nullable: true),
                    Budsjettaar = table.Column<int>(type: "int", nullable: false),
                    Kategori = table.Column<int>(type: "int", nullable: false),
                    Loep = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForeslaattSynlighet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EndrerFristId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DokumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forslag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Frister",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tittel = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Dato = table.Column<DateOnly>(type: "date", nullable: false),
                    Datopresisjon = table.Column<int>(type: "int", nullable: false),
                    Datokvalifikator = table.Column<int>(type: "int", nullable: true),
                    Sorteringsdag = table.Column<DateOnly>(type: "date", nullable: false),
                    Budsjettaar = table.Column<int>(type: "int", nullable: false),
                    Kategori = table.Column<int>(type: "int", nullable: false),
                    Loep = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Kilde = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DokumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Opphav = table.Column<int>(type: "int", nullable: false),
                    ForeslaattAv = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GjentaRegelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Frister", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gjentaksregler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Loep = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Kategori = table.Column<int>(type: "int", nullable: false),
                    Regeltype = table.Column<int>(type: "int", nullable: false),
                    Regelparametre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Valgaarssensitiv = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gjentaksregler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Synlighetsgrupper",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Navn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Aktiv = table.Column<bool>(type: "bit", nullable: false),
                    ErStandard = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Synlighetsgrupper", x => x.Id);
                    table.UniqueConstraint("AK_Synlighetsgrupper_Kode", x => x.Kode);
                });

            migrationBuilder.CreateTable(
                name: "Varsler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrukerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Tekst = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Begrunnelse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lest = table.Column<bool>(type: "bit", nullable: false),
                    Opprettet = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Varsler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UttrekksBevis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForslagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Felt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TolketVerdi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kildeutdrag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Konfidens = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UttrekksBevis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UttrekksBevis_Forslag_ForslagId",
                        column: x => x.ForslagId,
                        principalTable: "Forslag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BrukerGrupper",
                columns: table => new
                {
                    BrukerId = table.Column<string>(type: "nvarchar(128)", nullable: false),
                    GruppeKode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kilde = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrukerGrupper", x => new { x.BrukerId, x.GruppeKode });
                    table.ForeignKey(
                        name: "FK_BrukerGrupper_Brukere_BrukerId",
                        column: x => x.BrukerId,
                        principalTable: "Brukere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BrukerGrupper_Synlighetsgrupper_GruppeKode",
                        column: x => x.GruppeKode,
                        principalTable: "Synlighetsgrupper",
                        principalColumn: "Kode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FristSynlighet",
                columns: table => new
                {
                    FristId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GruppeKode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FristSynlighet", x => new { x.FristId, x.GruppeKode });
                    table.ForeignKey(
                        name: "FK_FristSynlighet_Frister_FristId",
                        column: x => x.FristId,
                        principalTable: "Frister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FristSynlighet_Synlighetsgrupper_GruppeKode",
                        column: x => x.GruppeKode,
                        principalTable: "Synlighetsgrupper",
                        principalColumn: "Kode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BehandledeDokumenter_Kilde_DokumentNokkel",
                table: "BehandledeDokumenter",
                columns: new[] { "Kilde", "DokumentNokkel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrukerGrupper_GruppeKode",
                table: "BrukerGrupper",
                column: "GruppeKode");

            migrationBuilder.CreateIndex(
                name: "IX_Forslag_EndrerFristId",
                table: "Forslag",
                column: "EndrerFristId");

            migrationBuilder.CreateIndex(
                name: "IX_Frister_Budsjettaar",
                table: "Frister",
                column: "Budsjettaar");

            migrationBuilder.CreateIndex(
                name: "IX_Frister_Sorteringsdag",
                table: "Frister",
                column: "Sorteringsdag");

            migrationBuilder.CreateIndex(
                name: "IX_FristSynlighet_GruppeKode",
                table: "FristSynlighet",
                column: "GruppeKode");

            migrationBuilder.CreateIndex(
                name: "IX_Synlighetsgrupper_Kode",
                table: "Synlighetsgrupper",
                column: "Kode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UttrekksBevis_ForslagId",
                table: "UttrekksBevis",
                column: "ForslagId");

            migrationBuilder.CreateIndex(
                name: "IX_Varsler_BrukerId",
                table: "Varsler",
                column: "BrukerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BehandledeDokumenter");

            migrationBuilder.DropTable(
                name: "BrukerGrupper");

            migrationBuilder.DropTable(
                name: "FristSynlighet");

            migrationBuilder.DropTable(
                name: "Gjentaksregler");

            migrationBuilder.DropTable(
                name: "UttrekksBevis");

            migrationBuilder.DropTable(
                name: "Varsler");

            migrationBuilder.DropTable(
                name: "Brukere");

            migrationBuilder.DropTable(
                name: "Frister");

            migrationBuilder.DropTable(
                name: "Synlighetsgrupper");

            migrationBuilder.DropTable(
                name: "Forslag");
        }
    }
}
