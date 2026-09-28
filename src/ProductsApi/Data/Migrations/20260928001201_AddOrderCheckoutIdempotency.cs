using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCheckoutIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutCartVersion",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_CheckoutCartVersion",
                table: "Orders",
                columns: new[] { "CustomerId", "CheckoutCartVersion" },
                unique: true,
                filter: "[CheckoutCartVersion] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId_CheckoutCartVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CheckoutCartVersion",
                table: "Orders");
        }
    }
}
