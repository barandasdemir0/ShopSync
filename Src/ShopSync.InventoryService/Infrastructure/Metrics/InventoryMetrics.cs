using System.Diagnostics.Metrics;

namespace ShopSync.InventoryService.Infrastructure.Metrics;

public sealed class InventoryMetrics:IDisposable
{

    public const string MeterName = "ShopSync.InventoryService"; 

    private readonly Meter _meter;


    // Toplam başarılı rezervasyon sayısı
    public Counter<long> ReservationsCompleted { get; }


    // Toplam başarısız rezervasyon sayısı
    public Counter<long> ReservationsFailed { get; }


    // Toplam süresi dolmuş (expired) rezervasyon sayısı
    public Counter<long> ReservationsExpired { get; }


    // Toplam onaylanan (confirmed) rezervasyon sayısı
    public Counter<long> ReservationsConfirmed { get; }


    // Bir rezervasyon işleminin ne kadar sürdüğü (milisaniye)
    public Histogram<double> ReserveDurationMs { get; }


    // Lock alma işleminin ne kadar sürdüğü (milisaniye)
    public Histogram<double> LockAcquisitionDurationMs { get; }


    public InventoryMetrics()
    {
        _meter = new Meter("ShopSync.InventoryService");
        ReservationsCompleted = _meter.CreateCounter<long>(
            "inventory.reservations.completed",
            description: "Toplam başarılı rezervasyon sayısı");


        ReservationsFailed = _meter.CreateCounter<long>(
            "inventory.reservations.failed",
            description: "Toplam başarısız rezervasyon sayısı");


        ReservationsExpired = _meter.CreateCounter<long>(
            "inventory.reservations.expired",
            description: "Toplam süresi dolmuş rezervasyon sayısı");


        ReservationsConfirmed = _meter.CreateCounter<long>(
            "inventory.reservations.confirmed",
            description: "Toplam onaylanan rezervasyon sayısı");


        ReserveDurationMs = _meter.CreateHistogram<double>(
            "inventory.reserve.duration.ms",
            unit: "ms",
            description: "Rezervasyon işlem süresi (milisaniye)");


        LockAcquisitionDurationMs = _meter.CreateHistogram<double>(
            "inventory.lock.acquisition.duration.ms",
            unit: "ms",
            description: "Lock alma süresi (milisaniye)");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
