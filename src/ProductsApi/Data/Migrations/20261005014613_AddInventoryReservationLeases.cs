using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservationLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_ProductId",
                table: "InventoryReservations");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "InventoryReservations",
                type: "datetime2",
                nullable: true);

            // Sessions created before leases used Stripe's 24-hour default.
            // The extra hour ensures the database never releases their stock
            // before the corresponding provider session is unable to pay.
            migrationBuilder.Sql(
                """
                UPDATE [InventoryReservations]
                SET [ExpiresAtUtc] = DATEADD(HOUR, 25, [CreatedAtUtc])
                WHERE [ExpiresAtUtc] IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "InventoryReservations",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductInventories_Reserved",
                table: "ProductInventories");

            migrationBuilder.DropColumn(
                name: "Reserved",
                table: "ProductInventories");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_ProductId_ExpiresAtUtc",
                table: "InventoryReservations",
                columns: new[] { "ProductId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_ProductId_ExpiresAtUtc",
                table: "InventoryReservations");

            migrationBuilder.AddColumn<int>(
                name: "Reserved",
                table: "ProductInventories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                DELETE FROM [InventoryReservations]
                WHERE [ExpiresAtUtc] <= SYSUTCDATETIME();

                UPDATE inventory
                SET [Reserved] = reservations.[Quantity]
                FROM [ProductInventories] AS inventory
                INNER JOIN
                (
                    SELECT [ProductId], SUM([Quantity]) AS [Quantity]
                    FROM [InventoryReservations]
                    GROUP BY [ProductId]
                ) AS reservations
                    ON reservations.[ProductId] = inventory.[ProductId];
                """);

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "InventoryReservations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductInventories_Reserved",
                table: "ProductInventories",
                sql: "[Reserved] >= 0 AND [Reserved] <= [OnHand]");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_ProductId",
                table: "InventoryReservations",
                column: "ProductId");
        }
    }
}
