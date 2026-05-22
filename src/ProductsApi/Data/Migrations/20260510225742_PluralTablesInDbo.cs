using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class PluralTablesInDbo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Category_Category_ParentCategoryId",
                schema: "catalog",
                table: "Category");

            migrationBuilder.DropForeignKey(
                name: "FK_Product_Category_CategoryId",
                schema: "catalog",
                table: "Product");

            migrationBuilder.DropForeignKey(
                name: "FK_Product_Category_SubCategoryId",
                schema: "catalog",
                table: "Product");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductAttribute_Product_ProductId",
                schema: "catalog",
                table: "ProductAttribute");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImage_Product_ProductId",
                schema: "catalog",
                table: "ProductImage");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPrice_Product_ProductId",
                schema: "catalog",
                table: "ProductPrice");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RawProductImport",
                schema: "catalog",
                table: "RawProductImport");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductPrice",
                schema: "catalog",
                table: "ProductPrice");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductImage",
                schema: "catalog",
                table: "ProductImage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductAttribute",
                schema: "catalog",
                table: "ProductAttribute");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Product",
                schema: "catalog",
                table: "Product");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Category",
                schema: "catalog",
                table: "Category");

            migrationBuilder.RenameTable(
                name: "RawProductImport",
                schema: "catalog",
                newName: "RawProductImports");

            migrationBuilder.RenameTable(
                name: "ProductPrice",
                schema: "catalog",
                newName: "ProductPrices");

            migrationBuilder.RenameTable(
                name: "ProductImage",
                schema: "catalog",
                newName: "ProductImages");

            migrationBuilder.RenameTable(
                name: "ProductAttribute",
                schema: "catalog",
                newName: "ProductAttributes");

            migrationBuilder.RenameTable(
                name: "Product",
                schema: "catalog",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "Category",
                schema: "catalog",
                newName: "Categories");

            migrationBuilder.RenameIndex(
                name: "IX_RawProductImport_Processed",
                table: "RawProductImports",
                newName: "IX_RawProductImports_Processed");

            migrationBuilder.RenameIndex(
                name: "IX_RawProductImport_ImportedAt",
                table: "RawProductImports",
                newName: "IX_RawProductImports_ImportedAt");

            migrationBuilder.RenameIndex(
                name: "IX_RawProductImport_ExternalProductId",
                table: "RawProductImports",
                newName: "IX_RawProductImports_ExternalProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrice_ProductId_CapturedAt",
                table: "ProductPrices",
                newName: "IX_ProductPrices_ProductId_CapturedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrice_DiscountPrice",
                table: "ProductPrices",
                newName: "IX_ProductPrices_DiscountPrice");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrice_CurrencyCode",
                table: "ProductPrices",
                newName: "IX_ProductPrices_CurrencyCode");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrice_ActualPrice",
                table: "ProductPrices",
                newName: "IX_ProductPrices_ActualPrice");

            migrationBuilder.RenameIndex(
                name: "IX_ProductImage_ProductId_IsPrimary",
                table: "ProductImages",
                newName: "IX_ProductImages_ProductId_IsPrimary");

            migrationBuilder.RenameIndex(
                name: "IX_ProductImage_ProductId",
                table: "ProductImages",
                newName: "IX_ProductImages_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductAttribute_ProductId",
                table: "ProductAttributes",
                newName: "IX_ProductAttributes_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductAttribute_AttributeName_AttributeValue",
                table: "ProductAttributes",
                newName: "IX_ProductAttributes_AttributeName_AttributeValue");

            migrationBuilder.RenameIndex(
                name: "IX_Product_SubCategoryId",
                table: "Products",
                newName: "IX_Products_SubCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Product_Name",
                table: "Products",
                newName: "IX_Products_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Product_ExternalProductId",
                table: "Products",
                newName: "IX_Products_ExternalProductId");

            migrationBuilder.RenameIndex(
                name: "IX_Product_CategoryId",
                table: "Products",
                newName: "IX_Products_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Product_Brand",
                table: "Products",
                newName: "IX_Products_Brand");

            migrationBuilder.RenameIndex(
                name: "IX_Category_ParentCategoryId",
                table: "Categories",
                newName: "IX_Categories_ParentCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Category_Name_ParentCategoryId",
                table: "Categories",
                newName: "IX_Categories_Name_ParentCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Category_Name",
                table: "Categories",
                newName: "IX_Categories_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RawProductImports",
                table: "RawProductImports",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductPrices",
                table: "ProductPrices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductImages",
                table: "ProductImages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductAttributes",
                table: "ProductAttributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Products",
                table: "Products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Categories",
                table: "Categories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductAttributes_Products_ProductId",
                table: "ProductAttributes",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                table: "ProductImages",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPrices_Products_ProductId",
                table: "ProductPrices",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_SubCategoryId",
                table: "Products",
                column: "SubCategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductAttributes_Products_ProductId",
                table: "ProductAttributes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                table: "ProductImages");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPrices_Products_ProductId",
                table: "ProductPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_SubCategoryId",
                table: "Products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RawProductImports",
                table: "RawProductImports");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Products",
                table: "Products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductPrices",
                table: "ProductPrices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductImages",
                table: "ProductImages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductAttributes",
                table: "ProductAttributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Categories",
                table: "Categories");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.RenameTable(
                name: "RawProductImports",
                newName: "RawProductImport",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Product",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "ProductPrices",
                newName: "ProductPrice",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "ProductImages",
                newName: "ProductImage",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "ProductAttributes",
                newName: "ProductAttribute",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "Categories",
                newName: "Category",
                newSchema: "catalog");

            migrationBuilder.RenameIndex(
                name: "IX_RawProductImports_Processed",
                schema: "catalog",
                table: "RawProductImport",
                newName: "IX_RawProductImport_Processed");

            migrationBuilder.RenameIndex(
                name: "IX_RawProductImports_ImportedAt",
                schema: "catalog",
                table: "RawProductImport",
                newName: "IX_RawProductImport_ImportedAt");

            migrationBuilder.RenameIndex(
                name: "IX_RawProductImports_ExternalProductId",
                schema: "catalog",
                table: "RawProductImport",
                newName: "IX_RawProductImport_ExternalProductId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_SubCategoryId",
                schema: "catalog",
                table: "Product",
                newName: "IX_Product_SubCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_Name",
                schema: "catalog",
                table: "Product",
                newName: "IX_Product_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Products_ExternalProductId",
                schema: "catalog",
                table: "Product",
                newName: "IX_Product_ExternalProductId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoryId",
                schema: "catalog",
                table: "Product",
                newName: "IX_Product_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_Brand",
                schema: "catalog",
                table: "Product",
                newName: "IX_Product_Brand");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrices_ProductId_CapturedAt",
                schema: "catalog",
                table: "ProductPrice",
                newName: "IX_ProductPrice_ProductId_CapturedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrices_DiscountPrice",
                schema: "catalog",
                table: "ProductPrice",
                newName: "IX_ProductPrice_DiscountPrice");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrices_CurrencyCode",
                schema: "catalog",
                table: "ProductPrice",
                newName: "IX_ProductPrice_CurrencyCode");

            migrationBuilder.RenameIndex(
                name: "IX_ProductPrices_ActualPrice",
                schema: "catalog",
                table: "ProductPrice",
                newName: "IX_ProductPrice_ActualPrice");

            migrationBuilder.RenameIndex(
                name: "IX_ProductImages_ProductId_IsPrimary",
                schema: "catalog",
                table: "ProductImage",
                newName: "IX_ProductImage_ProductId_IsPrimary");

            migrationBuilder.RenameIndex(
                name: "IX_ProductImages_ProductId",
                schema: "catalog",
                table: "ProductImage",
                newName: "IX_ProductImage_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductAttributes_ProductId",
                schema: "catalog",
                table: "ProductAttribute",
                newName: "IX_ProductAttribute_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductAttributes_AttributeName_AttributeValue",
                schema: "catalog",
                table: "ProductAttribute",
                newName: "IX_ProductAttribute_AttributeName_AttributeValue");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_ParentCategoryId",
                schema: "catalog",
                table: "Category",
                newName: "IX_Category_ParentCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_Name_ParentCategoryId",
                schema: "catalog",
                table: "Category",
                newName: "IX_Category_Name_ParentCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_Name",
                schema: "catalog",
                table: "Category",
                newName: "IX_Category_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RawProductImport",
                schema: "catalog",
                table: "RawProductImport",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Product",
                schema: "catalog",
                table: "Product",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductPrice",
                schema: "catalog",
                table: "ProductPrice",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductImage",
                schema: "catalog",
                table: "ProductImage",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductAttribute",
                schema: "catalog",
                table: "ProductAttribute",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Category",
                schema: "catalog",
                table: "Category",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Category_Category_ParentCategoryId",
                schema: "catalog",
                table: "Category",
                column: "ParentCategoryId",
                principalSchema: "catalog",
                principalTable: "Category",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Product_Category_CategoryId",
                schema: "catalog",
                table: "Product",
                column: "CategoryId",
                principalSchema: "catalog",
                principalTable: "Category",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Product_Category_SubCategoryId",
                schema: "catalog",
                table: "Product",
                column: "SubCategoryId",
                principalSchema: "catalog",
                principalTable: "Category",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductAttribute_Product_ProductId",
                schema: "catalog",
                table: "ProductAttribute",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImage_Product_ProductId",
                schema: "catalog",
                table: "ProductImage",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPrice_Product_ProductId",
                schema: "catalog",
                table: "ProductPrice",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "Product",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
