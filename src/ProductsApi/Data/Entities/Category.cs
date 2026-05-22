namespace ProductsApi.Data.Entities;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int? ParentCategoryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Category? ParentCategory { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ICollection<Product> SubCategoryProducts { get; set; } = new List<Product>();
}
