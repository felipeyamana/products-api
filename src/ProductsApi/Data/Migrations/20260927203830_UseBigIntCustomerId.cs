using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseBigIntCustomerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_Id",
                table: "Customers");

            migrationBuilder.RenameTable(
                name: "Customers",
                newName: "Customers_Guid");

            migrationBuilder.Sql(
                "EXEC sp_rename N'[dbo].[PK_Customers]', N'PK_Customers_Guid', N'OBJECT';");

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[Customers] ([UserId], [CreatedAtUtc])
                SELECT [Id], [CreatedAtUtc]
                FROM [dbo].[Customers_Guid];
                """);

            migrationBuilder.DropTable(
                name: "Customers_Guid");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId",
                table: "Customers",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_UserId",
                table: "Customers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [dbo].[Customers] WHERE [UserId] IS NULL)
                    THROW 51000, 'Cannot revert Customers to GUID primary keys while guest customers exist.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_UserId",
                table: "Customers");

            migrationBuilder.RenameTable(
                name: "Customers",
                newName: "Customers_BigInt");

            migrationBuilder.Sql(
                "EXEC sp_rename N'[dbo].[PK_Customers]', N'PK_Customers_BigInt', N'OBJECT';");

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[Customers] ([Id], [CreatedAtUtc])
                SELECT [UserId], [CreatedAtUtc]
                FROM [dbo].[Customers_BigInt];
                """);

            migrationBuilder.DropTable(
                name: "Customers_BigInt");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_Id",
                table: "Customers",
                column: "Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
