using ShopSync.Web.Dtos;

namespace ShopSync.Web.Models;

public sealed class OrderDetailViewModel
{
    public OrderResponseDto Order { get; set; } = new();
    // Siparişin bekleyip beklemediğini (işlem butonlarını göstermek için) Model kontrol eder
    public bool IsPending => Order.Status == "PENDING";
    // CSS sınıfını Model belirler
    public string BadgeClass => Order.Status switch
    {
        "PENDING" => "badge-pending",
        "CONFIRMED" => "badge-confirmed",
        "CANCELLED" => "badge-cancelled",
        "EXPIRED" => "badge-expired",
        _ => "badge-pending"
    };
}
