using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SonicPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHotspotsAndGrouping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "hotspot_id",
                table: "detections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "location",
                table: "detections",
                type: "geography(Point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_status",
                table: "detections",
                type: "text",
                nullable: true);

            // Explicitly populate every existing row (not just NULLs) before
            // closing the column to NOT NULL. Rows saved before grouping
            // existed are treated as already processed, so startup recovery
            // doesn't sweep the entire historical table into grouping on
            // first launch.
            migrationBuilder.Sql(
                "UPDATE detections SET processing_status = 'Processed';");

            migrationBuilder.AlterColumn<string>(
                name: "processing_status",
                table: "detections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Backfill the geography column for rows that predate it, from
            // their existing lat/lon columns.
            migrationBuilder.Sql(
                "UPDATE detections SET location = " +
                "ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography " +
                "WHERE location IS NULL;");

            migrationBuilder.CreateTable(
                name: "hotspots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    radius_meters = table.Column<double>(type: "double precision", nullable: false),
                    confidence = table.Column<int>(type: "integer", nullable: false),
                    device_count = table.Column<int>(type: "integer", nullable: false),
                    first_received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    centroid_latitude = table.Column<double>(type: "double precision", nullable: false),
                    centroid_longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hotspots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detections_hotspot_id",
                table: "detections",
                column: "hotspot_id");

            migrationBuilder.CreateIndex(
                name: "IX_detections_location",
                table: "detections",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_hotspots_last_received_at_utc",
                table: "hotspots",
                column: "last_received_at_utc");

            migrationBuilder.AddForeignKey(
                name: "FK_detections_hotspots_hotspot_id",
                table: "detections",
                column: "hotspot_id",
                principalTable: "hotspots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_detections_hotspots_hotspot_id",
                table: "detections");

            migrationBuilder.DropTable(
                name: "hotspots");

            migrationBuilder.DropIndex(
                name: "IX_detections_hotspot_id",
                table: "detections");

            migrationBuilder.DropIndex(
                name: "IX_detections_location",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "hotspot_id",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "location",
                table: "detections");

            migrationBuilder.DropColumn(
                name: "processing_status",
                table: "detections");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
