using CsvHelper.Configuration;

namespace ProductCatalogImport.Csv;

public sealed class ElectronicsRowMap : ClassMap<ElectronicsRow>
{
    public ElectronicsRowMap()
    {
        Map(m => m.Name).Name("name");
        Map(m => m.MainCategory).Name("main_category");
        Map(m => m.SubCategory).Name("sub_category");
        Map(m => m.Image).Name("image");
        Map(m => m.Link).Name("link");
        Map(m => m.Ratings).Name("ratings");
        Map(m => m.NoOfRatings).Name("no_of_ratings");
        Map(m => m.DiscountPrice).Name("discount_price");
        Map(m => m.ActualPrice).Name("actual_price");
    }
}
