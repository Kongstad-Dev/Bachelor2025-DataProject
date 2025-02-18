using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Bach2025nortec.Migrations
{
    /// <inheritdoc />
    public partial class DataEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataEntities");
        }
    }
}
