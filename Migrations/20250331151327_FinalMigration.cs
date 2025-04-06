using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace BlazorTest.Migrations
{
    /// <inheritdoc />
    public partial class FinalMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bank",
                columns: table => new
                {
                    bankId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank", x => x.bankId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "laundromat",
                columns: table => new
                {
                    kId = table.Column<string>(type: "varchar(255)", nullable: false),
                    externalId = table.Column<string>(type: "longtext", nullable: true),
                    bank = table.Column<string>(type: "longtext", nullable: true),
                    bankId = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "longtext", nullable: true),
                    zip = table.Column<string>(type: "longtext", nullable: true),
                    longitude = table.Column<float>(type: "float", nullable: false),
                    latitude = table.Column<float>(type: "float", nullable: false),
                    lastFetchDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laundromat", x => x.kId);
                    table.ForeignKey(
                        name: "FK_laundromat_bank_bankId",
                        column: x => x.bankId,
                        principalTable: "bank",
                        principalColumn: "bankId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
                name: "IX_laundromat_bankId",
                table: "laundromat",
                column: "bankId");

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

            migrationBuilder.DropTable(
                name: "laundromat");

            migrationBuilder.DropTable(
                name: "bank");
        }
    }
}
