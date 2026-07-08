using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SonicPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDetectionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "detections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peak_dbfs = table.Column<double>(type: "double precision", nullable: false),
                    gps_accuracy = table.Column<double>(type: "double precision", nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    peak_time_client = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detections_device_id_sequence_number",
                table: "detections",
                columns: new[] { "device_id", "sequence_number" });

            migrationBuilder.CreateIndex(
                name: "IX_detections_received_at_utc",
                table: "detections",
                column: "received_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_detections_sequence_number",
                table: "detections",
                column: "sequence_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detections");
        }
    }
}
