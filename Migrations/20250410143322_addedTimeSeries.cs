using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorTest.Migrations
{
    /// <inheritdoc />
    public partial class addedTimeSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvailableTimeSeriesData",
                table: "laundromat_stats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RevenueTimeSeriesData",
                table: "laundromat_stats",
                type: "json",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "TransactionCountTimeSeriesData",
                table: "laundromat_stats",
                type: "json",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableTimeSeriesData",
                table: "laundromat_stats");

            migrationBuilder.DropColumn(
                name: "RevenueTimeSeriesData",
                table: "laundromat_stats");

            migrationBuilder.DropColumn(
                name: "TransactionCountTimeSeriesData",
                table: "laundromat_stats");
        }
    }
}
