using ShopSync.Web.Dtos;

namespace ShopSync.Web.Models;

public sealed class OrderIndexViewModel
{
    public List<OrderResponseDto> Orders { get; set; } = new();

    // Sayfalama Bilgileri
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public string StatusFilter { get; set; } = string.Empty;
    // View'ın yapması gereken matematiği Model yapıyor
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    // Rozet (Badge) CSS sınıfını belirleyen metot
    public string GetBadgeClass(string status) => status switch
    {
        "PENDING" => "badge-pending",
        "CONFIRMED" => "badge-confirmed",
        "CANCELLED" => "badge-cancelled",
        "EXPIRED" => "badge-expired",
        _ => "badge-pending"
    };
}
