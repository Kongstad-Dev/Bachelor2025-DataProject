using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorTest.Migrations
{
    /// <inheritdoc />
    public partial class addedStartPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DryerStartPrice",
                table: "laundromat_stats",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WasherStartPrice",
                table: "laundromat_stats",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DryerStartPrice",
                table: "laundromat_stats");

            migrationBuilder.DropColumn(
                name: "WasherStartPrice",
                table: "laundromat_stats");
        }
    }
}
