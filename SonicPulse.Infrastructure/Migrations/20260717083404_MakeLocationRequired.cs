using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SonicPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeLocationRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Point>(
                name: "location",
                table: "detections",
                type: "geography(Point, 4326)",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geography(Point, 4326)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Point>(
                name: "location",
                table: "detections",
                type: "geography(Point, 4326)",
                nullable: true,
                oldClrType: typeof(Point),
                oldType: "geography(Point, 4326)");
        }
    }
}
