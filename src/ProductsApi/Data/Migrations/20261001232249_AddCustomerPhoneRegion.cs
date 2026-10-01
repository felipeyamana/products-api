using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPhoneRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "Customers",
                newName: "PhoneNumberE164");

            migrationBuilder.Sql("""
                UPDATE [Customers]
                SET [PhoneNumberE164] = REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE([PhoneNumberE164], ' ', ''),
                                '(', ''),
                            ')', ''),
                        '-', ''),
                    '.', '')
                WHERE [PhoneNumberE164] IS NOT NULL;

                IF EXISTS (
                    SELECT 1
                    FROM [Customers]
                    WHERE [PhoneNumberE164] IS NOT NULL
                      AND (
                          LEN([PhoneNumberE164]) NOT BETWEEN 3 AND 16
                          OR LEFT([PhoneNumberE164], 1) <> '+'
                          OR SUBSTRING([PhoneNumberE164], 2, 1) = '0'
                          OR SUBSTRING([PhoneNumberE164], 2, 15)
                              COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%'
                      )
                )
                    THROW 51000, 'Existing customer phone numbers must be converted to E.164 format before this migration can continue.', 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumberE164",
                table: "Customers",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingPhoneRegionCode",
                table: "Orders",
                type: "char(2)",
                unicode: false,
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneRegionCode",
                table: "Customers",
                type: "char(2)",
                unicode: false,
                fixedLength: true,
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingPhoneRegionCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PhoneRegionCode",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumberE164",
                table: "Customers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(16)",
                oldUnicode: false,
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "PhoneNumberE164",
                table: "Customers",
                newName: "PhoneNumber");
        }
    }
}
