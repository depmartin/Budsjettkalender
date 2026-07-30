using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KalendereksportKlokkeslett : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "Klokkeslett",
                table: "Frister",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Klokkeslett",
                table: "Frister");
        }
    }
}
