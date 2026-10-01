using ProductsApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
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
        ConfigureCustomer(modelBuilder);
        ConfigureCustomerAddress(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigureOrderItem(modelBuilder);
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

    private static void ConfigureCustomer(ModelBuilder modelBuilder)
    {
        var customer = modelBuilder.Entity<Customer>();

        customer.ToTable("Customers");
        customer.HasKey(x => x.Id);
        customer.Property(x => x.FirstName).HasMaxLength(100);
        customer.Property(x => x.LastName).HasMaxLength(100);
        customer.Property(x => x.PhoneNumberE164)
            .HasMaxLength(16)
            .IsUnicode(false);
        customer.Property(x => x.PhoneRegionCode)
            .HasMaxLength(2)
            .IsFixedLength()
            .IsUnicode(false);
        customer.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        customer.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        customer.Property(x => x.RowVersion).IsRowVersion();
        customer.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");
        customer.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Customer>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCustomerAddress(ModelBuilder modelBuilder)
    {
        var address = modelBuilder.Entity<CustomerAddress>();

        address.ToTable("CustomerAddresses", table =>
        {
            table.HasCheckConstraint(
                "CK_CustomerAddresses_RequiredFields",
                "LEN(LTRIM(RTRIM([RecipientName]))) > 0 " +
                "AND LEN(LTRIM(RTRIM([AddressLine1]))) > 0 " +
                "AND LEN(LTRIM(RTRIM([City]))) > 0 " +
                "AND LEN(LTRIM(RTRIM([Region]))) > 0 " +
                "AND LEN(LTRIM(RTRIM([PostalCode]))) > 0 " +
                "AND LEN(LTRIM(RTRIM([CountryCode]))) = 2");
        });
        address.HasKey(x => x.Id);
        address.Property(x => x.PublicId)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();
        address.Property(x => x.Label)
            .HasMaxLength(50);
        address.Property(x => x.RecipientName)
            .HasMaxLength(200)
            .IsRequired();
        address.Property(x => x.PhoneNumber)
            .HasMaxLength(32);
        address.Property(x => x.AddressLine1)
            .HasMaxLength(200)
            .IsRequired();
        address.Property(x => x.AddressLine2)
            .HasMaxLength(200);
        address.Property(x => x.City)
            .HasMaxLength(100)
            .IsRequired();
        address.Property(x => x.Region)
            .HasMaxLength(100)
            .IsRequired();
        address.Property(x => x.PostalCode)
            .HasMaxLength(30)
            .IsRequired();
        address.Property(x => x.CountryCode)
            .HasMaxLength(2)
            .IsFixedLength()
            .IsRequired();
        address.Property(x => x.IsDefault)
            .HasDefaultValue(false);
        address.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");
        address.Property(x => x.UpdatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");
        address.Property(x => x.RowVersion)
            .IsRowVersion();
        address.HasOne(x => x.Customer)
            .WithMany(x => x.Addresses)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        address.HasIndex(x => x.PublicId)
            .IsUnique();
        address.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
        address.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
    }

    private static void ConfigureOrder(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();

        order.ToTable("Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_Orders_Amounts",
                "[Subtotal] >= 0 AND [DiscountTotal] >= 0 AND [ShippingTotal] >= 0 AND [TaxTotal] >= 0 AND [GrandTotal] >= 0");
        });
        order.HasKey(x => x.Id);
        order.Property(x => x.PublicId)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();
        order.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(OrderStatus.Pending);
        order.Property(x => x.CustomerEmail)
            .HasMaxLength(320)
            .IsRequired();
        order.Property(x => x.RecipientName)
            .HasMaxLength(200)
            .IsRequired();
        order.Property(x => x.ShippingAddressLine1)
            .HasMaxLength(200)
            .IsRequired();
        order.Property(x => x.ShippingAddressLine2)
            .HasMaxLength(200);
        order.Property(x => x.ShippingPhoneNumber)
            .HasMaxLength(32);
        order.Property(x => x.ShippingPhoneRegionCode)
            .HasMaxLength(2)
            .IsFixedLength()
            .IsUnicode(false);
        order.Property(x => x.ShippingCity)
            .HasMaxLength(100)
            .IsRequired();
        order.Property(x => x.ShippingRegion)
            .HasMaxLength(100)
            .IsRequired();
        order.Property(x => x.ShippingPostalCode)
            .HasMaxLength(30)
            .IsRequired();
        order.Property(x => x.ShippingCountryCode)
            .HasMaxLength(2)
            .IsFixedLength()
            .IsRequired();
        order.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();
        order.Property(x => x.Subtotal).HasPrecision(18, 2);
        order.Property(x => x.DiscountTotal).HasPrecision(18, 2);
        order.Property(x => x.ShippingTotal).HasPrecision(18, 2);
        order.Property(x => x.TaxTotal).HasPrecision(18, 2);
        order.Property(x => x.GrandTotal).HasPrecision(18, 2);
        order.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        order.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        order.HasOne(x => x.Customer)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        order.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        order.HasIndex(x => x.PublicId).IsUnique();
        order.HasIndex(x => new { x.CustomerId, x.CheckoutCartVersion })
            .IsUnique()
            .HasFilter("[CheckoutCartVersion] IS NOT NULL");
        order.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
        order.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }

    private static void ConfigureOrderItem(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<OrderItem>();

        item.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] BETWEEN 1 AND 999");
            table.HasCheckConstraint(
                "CK_OrderItems_Amounts",
                "[UnitPrice] >= 0 AND [DiscountAmount] >= 0 AND [LineTotal] >= 0");
        });
        item.HasKey(x => x.Id);
        item.Property(x => x.ProductName)
            .HasMaxLength(500)
            .IsRequired();
        item.Property(x => x.ProductExternalId)
            .HasMaxLength(100);
        item.Property(x => x.UnitPrice).HasPrecision(18, 2);
        item.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        item.Property(x => x.LineTotal).HasPrecision(18, 2);
        item.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
        item.HasIndex(x => x.OrderId);
        item.HasIndex(x => x.ProductId);
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
