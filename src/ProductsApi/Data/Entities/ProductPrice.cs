namespace ProductsApi.Data.Entities;

public class ProductPrice
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string? StoreName { get; set; }

    public decimal ActualPrice { get; set; }

    public decimal? DiscountPrice { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public string? ProductUrl { get; set; }

    public DateTime CapturedAt { get; set; }

    public Product Product { get; set; } = null!;
}
