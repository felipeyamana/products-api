using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductInventories",
                columns: table => new
                {
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    OnHand = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Reserved = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductInventories", x => x.ProductId);
                    table.CheckConstraint("CK_ProductInventories_OnHand", "[OnHand] >= 0");
                    table.CheckConstraint("CK_ProductInventories_Reserved", "[Reserved] >= 0 AND [Reserved] <= [OnHand]");
                    table.ForeignKey(
                        name: "FK_ProductInventories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductInventories");
        }
    }
}
