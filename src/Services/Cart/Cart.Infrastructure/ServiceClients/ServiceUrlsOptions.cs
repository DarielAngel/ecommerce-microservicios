namespace Ecommerce.Cart.Infrastructure.ServiceClients;

public class ServiceUrlsOptions
{
    public const string SectionName = "ServiceUrls";

    public string CatalogBaseUrl { get; set; } = "http://catalog-service:8080/";
    public string InventoryBaseUrl { get; set; } = "http://inventory-service:8080/";
}
