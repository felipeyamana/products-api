using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    CREATE FULLTEXT CATALOG [ProductsFullTextCatalog]
                        WITH ACCENT_SENSITIVITY = OFF
                        AS DEFAULT;

                    CREATE FULLTEXT INDEX ON [dbo].[Products]
                    (
                        [Name] LANGUAGE 1033,
                        [Brand] LANGUAGE 1033,
                        [Description] LANGUAGE 1033
                    )
                    KEY INDEX [PK_Products]
                    ON [ProductsFullTextCatalog]
                    WITH CHANGE_TRACKING AUTO;
                END;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[Products]'))
                    DROP FULLTEXT INDEX ON [dbo].[Products];

                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_catalogs
                    WHERE name = N'ProductsFullTextCatalog')
                    DROP FULLTEXT CATALOG [ProductsFullTextCatalog];
                """,
                suppressTransaction: true);
        }
    }
}
