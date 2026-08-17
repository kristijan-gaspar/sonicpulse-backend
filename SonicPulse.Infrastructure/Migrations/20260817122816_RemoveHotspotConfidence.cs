using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonicPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHotspotConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confidence",
                table: "hotspots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "confidence",
                table: "hotspots",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
