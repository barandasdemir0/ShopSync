using ShopSync.Web.Dtos;

namespace ShopSync.Web.Models;

public sealed class InventoryIndexViewModel
{
    public GetStockResponseDto Stock { get; set; } = new();
    public ForecastResponseDto Forecast { get; set; } = new();
    public int Days { get; set; } = 7;
}
