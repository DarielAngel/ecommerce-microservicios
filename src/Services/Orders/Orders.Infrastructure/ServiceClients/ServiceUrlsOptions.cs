namespace Ecommerce.Orders.Infrastructure.ServiceClients;

public class ServiceUrlsOptions
{
    public const string SectionName = "ServiceUrls";

    public string CartBaseUrl { get; set; } = "http://cart-service:8080/";
    public string InventoryBaseUrl { get; set; } = "http://inventory-service:8080/";
    public string PaymentsBaseUrl { get; set; } = "http://payments-service:8080/";
    public string PromotionsBaseUrl { get; set; } = "http://promotions-service:8080/";
    public string LoyaltyBaseUrl { get; set; } = "http://loyalty-service:8080/";
}
