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
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') <> 1
                    THROW 50000, 'SQL Server Full-Text Search is not installed.', 1;

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
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP FULLTEXT INDEX ON [dbo].[Products];
                DROP FULLTEXT CATALOG [ProductsFullTextCatalog];
                """,
                suppressTransaction: true);
        }
    }
}
