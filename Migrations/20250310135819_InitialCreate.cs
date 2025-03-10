using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Bach2025nortec.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    bId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank", x => x.bId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DataEntities",
                columns: table => new
                {
                    kId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    externalId = table.Column<int>(type: "int", nullable: false),
                    transactionsType = table.Column<int>(type: "int", nullable: false),
                    unittype = table.Column<int>(type: "int", nullable: false),
                    unitName = table.Column<string>(type: "longtext", nullable: false),
                    program = table.Column<int>(type: "int", nullable: false),
                    prewash = table.Column<int>(type: "int", nullable: false),
                    programtype = table.Column<int>(type: "int", nullable: false),
                    temperature = table.Column<int>(type: "int", nullable: false),
                    spin = table.Column<int>(type: "int", nullable: false),
                    soapBrand = table.Column<int>(type: "int", nullable: false),
                    dirty = table.Column<int>(type: "int", nullable: false),
                    rinse = table.Column<int>(type: "int", nullable: false),
                    minuts = table.Column<int>(type: "int", nullable: false),
                    seconds = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<int>(type: "int", nullable: false),
                    currency = table.Column<string>(type: "longtext", nullable: false),
                    user = table.Column<string>(type: "longtext", nullable: false),
                    debug = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataEntities", x => x.kId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "laundromat",
                columns: table => new
                {
                    kId = table.Column<string>(type: "varchar(255)", nullable: false),
                    externalId = table.Column<string>(type: "longtext", nullable: true),
                    bank = table.Column<string>(type: "longtext", nullable: true),
                    bId = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "longtext", nullable: true),
                    zip = table.Column<string>(type: "longtext", nullable: true),
                    longitude = table.Column<float>(type: "float", nullable: false),
                    latitude = table.Column<float>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laundromat", x => x.kId);
                    table.ForeignKey(
                        name: "FK_laundromat_bank_bId",
                        column: x => x.bId,
                        principalTable: "bank",
                        principalColumn: "bId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_laundromat_bId",
                table: "laundromat",
                column: "bId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataEntities");

            migrationBuilder.DropTable(
                name: "laundromat");

            migrationBuilder.DropTable(
                name: "bank");
        }
    }
}
