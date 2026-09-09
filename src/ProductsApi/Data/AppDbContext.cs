using ProductsApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Data;

public class AppDbContext : DbContext
{
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();

    public DbSet<RawProductImport> RawProductImports => Set<RawProductImport>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCategory(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureProductImage(modelBuilder);
        ConfigureProductPrice(modelBuilder);
        ConfigureProductAttribute(modelBuilder);
        ConfigureRawProductImport(modelBuilder);
        ConfigureCart(modelBuilder);
        ConfigureCartItem(modelBuilder);
    }

    private static void ConfigureCart(ModelBuilder modelBuilder)
    {
        var cart = modelBuilder.Entity<Cart>();

        cart.ToTable("Carts");
        cart.HasKey(x => x.UserId);
        cart.Property(x => x.UserId).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        cart.Property(x => x.Version).IsConcurrencyToken();
        cart.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureCartItem(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<CartItem>();

        item.ToTable("CartItems", table => table.HasCheckConstraint("CK_CartItems_Quantity", "[Quantity] BETWEEN 1 AND 99"));
        item.HasKey(x => x.Id);
        item.Property(x => x.UserId).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        item.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        item.Property(x => x.UnitPriceAtAddition).HasPrecision(18, 2);
        item.Property(x => x.CurrencyAtAddition).HasMaxLength(3).IsRequired();
        // No product FK: deleted catalog products must remain visible in saved carts.
    }

    private static void ConfigureCategory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Category>();

        entity.ToTable("Categories");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.Name);

        entity.HasIndex(x => new { x.Name, x.ParentCategoryId }).IsUnique();
    }

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Product>();

        entity.ToTable("Products");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name)
            .HasMaxLength(500)
            .IsRequired();

        entity.Property(x => x.Brand)
            .HasMaxLength(150);

        entity.Property(x => x.SourceUrl)
            .HasMaxLength(1000);

        entity.Property(x => x.ExternalProductId)
            .HasMaxLength(100);

        entity.Property(x => x.AverageRating)
            .HasPrecision(3, 2);

        entity.Property(x => x.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.Property(x => x.IsActive)
            .HasDefaultValue(true);

        entity.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.SubCategory)
            .WithMany(x => x.SubCategoryProducts)
            .HasForeignKey(x => x.SubCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.Name);

        entity.HasIndex(x => x.Brand);

        entity.HasIndex(x => x.CategoryId);

        entity.HasIndex(x => x.SubCategoryId);

        entity.HasIndex(x => x.ExternalProductId)
            .IsUnique()
            .HasFilter("[ExternalProductId] IS NOT NULL");
    }

    private static void ConfigureProductImage(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductImage>();

        entity.ToTable("ProductImages");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.ImageUrl)
            .HasMaxLength(1000)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.Product)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.ProductId);

        entity.HasIndex(x => new { x.ProductId, x.IsPrimary });
    }

    private static void ConfigureProductPrice(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductPrice>();

        entity.ToTable("ProductPrices");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.StoreName)
            .HasMaxLength(150);

        entity.Property(x => x.ActualPrice)
            .HasPrecision(18, 2);

        entity.Property(x => x.DiscountPrice)
            .HasPrecision(18, 2);

        entity.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        entity.Property(x => x.ProductUrl)
            .HasMaxLength(1000);

        entity.Property(x => x.CapturedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.Product)
            .WithMany(x => x.Prices)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.ProductId, x.CapturedAt });

        entity.HasIndex(x => x.CurrencyCode);

        entity.HasIndex(x => x.ActualPrice);

        entity.HasIndex(x => x.DiscountPrice);
    }

    private static void ConfigureProductAttribute(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductAttribute>();

        entity.ToTable("ProductAttributes");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.AttributeName)
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(x => x.AttributeValue)
            .HasMaxLength(500)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.Product)
            .WithMany(x => x.Attributes)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.ProductId);

        entity.HasIndex(x => new { x.AttributeName, x.AttributeValue });
    }

    private static void ConfigureRawProductImport(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RawProductImport>();

        entity.ToTable("RawProductImports");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.SourceName)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.ExternalProductId)
            .HasMaxLength(100);

        entity.Property(x => x.RawJson)
            .IsRequired();

        entity.Property(x => x.ImportedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.Property(x => x.Processed)
            .HasDefaultValue(false);

        entity.HasIndex(x => x.Processed);

        entity.HasIndex(x => x.ImportedAt);

        entity.HasIndex(x => x.ExternalProductId);
    }
}
