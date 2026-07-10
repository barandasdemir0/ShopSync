using ShopSync.Web.Dtos;

namespace ShopSync.Web.Models;

public sealed class AnalyticsViewModel
{
    public OrderAnalyticsResponseDto Data { get; set; } = new();
    // Pasta grafik yüzdeleri
    public double ConfirmPct => Data.TotalOrders > 0 ? Math.Round((double)Data.ConfirmedCount / Data.TotalOrders * 100, 1) : 0;
    public double CancelPct => Data.TotalOrders > 0 ? Math.Round((double)Data.CancelledCount / Data.TotalOrders * 100, 1) : 0;
    public double PendingPct => Data.TotalOrders > 0 ? Math.Round((double)Data.PendingCount / Data.TotalOrders * 100, 1) : 0;
    public double ExpiredPct => Data.TotalOrders > 0 ? Math.Round((double)Data.ExpiredCount / Data.TotalOrders * 100, 1) : 0;
    // Sütun grafik (Bar chart) yükseklikleri
    private double MaxCount => Math.Max(1, Math.Max(Data.ConfirmedCount, Math.Max(Data.CancelledCount, Math.Max(Data.PendingCount, Data.ExpiredCount))));
    public double ConfirmH => Data.ConfirmedCount / MaxCount * 100;
    public double CancelH => Data.CancelledCount / MaxCount * 100;
    public double PendingH => Data.PendingCount / MaxCount * 100;
    public double ExpiredH => Data.ExpiredCount / MaxCount * 100;
}
