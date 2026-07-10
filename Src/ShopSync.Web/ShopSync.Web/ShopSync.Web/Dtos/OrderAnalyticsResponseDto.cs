namespace ShopSync.Web.Dtos;


public sealed class OrderAnalyticsResponseDto
{
    // Genel İstatistikler
    public int TotalOrders { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CancelledCount { get; set; }
    public int ExpiredCount { get; set; }
    // Başarı/Başarısızlık Oranları
    public double ConfirmationRate { get; set; }  // Onaylanan / Toplam (%)
    public double CancellationRate { get; set; }  // İptal edilen / Toplam (%)
    public double ExpirationRate { get; set; }    // Süresi dolan / Toplam (%)

    // Zaman Metrikleri (saniye cinsinden)
    public double AverageTimeToConfirmSeconds { get; set; }   // Oluşturma → Onay süresi
    public double AverageTimeToExpireSeconds { get; set; }    // Oluşturma → Expire süresi
    public double AverageTimeToCancelSeconds{ get; set; }   // Oluşturma → İptal süresi

    // Dead Letter Queue
    public int DeadLetterCount { get; set; }
    // Analiz aralığı
    public DateTime AnalyzedFrom { get; set; }
    public DateTime AnalyzedTo { get; set; }

    public string PeakReservationTime { get; set; } = string.Empty;

}
