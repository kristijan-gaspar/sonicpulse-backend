using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonicPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHotspotCascadeDelete_fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_detections_hotspots_hotspot_id",
                table: "detections");

            migrationBuilder.AddForeignKey(
                name: "FK_detections_hotspots_hotspot_id",
                table: "detections",
                column: "hotspot_id",
                principalTable: "hotspots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_detections_hotspots_hotspot_id",
                table: "detections");

            migrationBuilder.AddForeignKey(
                name: "FK_detections_hotspots_hotspot_id",
                table: "detections",
                column: "hotspot_id",
                principalTable: "hotspots",
                principalColumn: "Id");
        }
    }
}
