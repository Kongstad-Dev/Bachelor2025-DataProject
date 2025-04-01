using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorTest.Migrations
{
    /// <inheritdoc />
    public partial class AddedNameInLaundromatStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LaundromatName",
                table: "laundromat_stats",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LaundromatName",
                table: "laundromat_stats");
        }
    }
}
