using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorTest.Migrations
{
    /// <inheritdoc />
    public partial class addedBankId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "locationId",
                table: "laundromat",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "locationId",
                table: "laundromat");
        }
    }
}
