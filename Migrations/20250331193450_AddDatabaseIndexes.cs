using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorTest.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_transaction_LaundromatId",
                table: "transaction",
                newName: "IX_Transaction_LaundromatId");

            migrationBuilder.RenameIndex(
                name: "IX_laundromat_bId",
                table: "laundromat",
                newName: "IX_Laundromat_BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Amount",
                table: "transaction",
                column: "amount");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_LaundromatId_Date",
                table: "transaction",
                columns: new[] { "LaundromatId", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_ProgramType",
                table: "transaction",
                column: "programType");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Soap",
                table: "transaction",
                column: "soap");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Temperature",
                table: "transaction",
                column: "temperature");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_UnitType",
                table: "transaction",
                column: "unitType");

            migrationBuilder.CreateIndex(
                name: "IX_Laundromat_Coordinates",
                table: "laundromat",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Laundromat_LastFetchDate",
                table: "laundromat",
                column: "lastFetchDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_Amount",
                table: "transaction");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_LaundromatId_Date",
                table: "transaction");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_ProgramType",
                table: "transaction");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_Soap",
                table: "transaction");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_Temperature",
                table: "transaction");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_UnitType",
                table: "transaction");

            migrationBuilder.DropIndex(
                name: "IX_Laundromat_Coordinates",
                table: "laundromat");

            migrationBuilder.DropIndex(
                name: "IX_Laundromat_LastFetchDate",
                table: "laundromat");

            migrationBuilder.RenameIndex(
                name: "IX_Transaction_LaundromatId",
                table: "transaction",
                newName: "IX_transaction_LaundromatId");

            migrationBuilder.RenameIndex(
                name: "IX_Laundromat_BankId",
                table: "laundromat",
                newName: "IX_laundromat_bId");
        }
    }
}
