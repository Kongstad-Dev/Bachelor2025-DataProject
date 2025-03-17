using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bach2025nortec.Migrations
{
    /// <inheritdoc />
    public partial class AddAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "lastFetchDate",
                table: "laundromat",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transaction",
                columns: table => new
                {
                    kId = table.Column<string>(type: "varchar(255)", nullable: false),
                    date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    transactionType = table.Column<int>(type: "int", nullable: false),
                    unitType = table.Column<int>(type: "int", nullable: false),
                    unitName = table.Column<string>(type: "longtext", nullable: true),
                    program = table.Column<int>(type: "int", nullable: false),
                    prewash = table.Column<int>(type: "int", nullable: false),
                    programType = table.Column<int>(type: "int", nullable: false),
                    temperature = table.Column<int>(type: "int", nullable: false),
                    spin = table.Column<int>(type: "int", nullable: false),
                    soap = table.Column<int>(type: "int", nullable: false),
                    soapBrand = table.Column<int>(type: "int", nullable: false),
                    dirty = table.Column<int>(type: "int", nullable: false),
                    rinse = table.Column<int>(type: "int", nullable: false),
                    minuts = table.Column<int>(type: "int", nullable: false),
                    seconds = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<int>(type: "int", nullable: false),
                    currency = table.Column<string>(type: "longtext", nullable: true),
                    user = table.Column<string>(type: "longtext", nullable: true),
                    debug = table.Column<string>(type: "longtext", nullable: true),
                    LaundromatId = table.Column<string>(type: "varchar(255)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction", x => x.kId);
                    table.ForeignKey(
                        name: "FK_transaction_laundromat_LaundromatId",
                        column: x => x.LaundromatId,
                        principalTable: "laundromat",
                        principalColumn: "kId");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_LaundromatId",
                table: "transaction",
                column: "LaundromatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction");

            migrationBuilder.DropColumn(
                name: "lastFetchDate",
                table: "laundromat");
        }
    }
}
