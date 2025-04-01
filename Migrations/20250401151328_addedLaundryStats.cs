using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BlazorTest.Migrations
{
    /// <inheritdoc />
    public partial class addedLaundryStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "laundromat_stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    LaundromatId = table.Column<string>(type: "varchar(255)", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    PeriodKey = table.Column<string>(type: "varchar(255)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTransactions = table.Column<int>(type: "int", nullable: false),
                    WashingMachineTransactions = table.Column<int>(type: "int", nullable: false),
                    DryerTransactions = table.Column<int>(type: "int", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laundromat_stats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_laundromat_stats_laundromat_LaundromatId",
                        column: x => x.LaundromatId,
                        principalTable: "laundromat",
                        principalColumn: "kId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LaundromatStats_Composite",
                table: "laundromat_stats",
                columns: new[] { "LaundromatId", "PeriodType", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "IX_LaundromatStats_LaundromatId",
                table: "laundromat_stats",
                column: "LaundromatId");

            migrationBuilder.CreateIndex(
                name: "IX_LaundromatStats_PeriodKey",
                table: "laundromat_stats",
                column: "PeriodKey");

            migrationBuilder.CreateIndex(
                name: "IX_LaundromatStats_PeriodType",
                table: "laundromat_stats",
                column: "PeriodType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "laundromat_stats");
        }
    }
}
