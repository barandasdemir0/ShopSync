namespace ShopSync.InventoryService.Configuration;

public sealed class ReconciliationJobSettings
{
    // Reconciliation job'un çalıştırılma aralığı (dakika cinsinden)
    public int IntervalMinutes { get; set; } = 60;
}
// bu sınıfın amacı mongodbdeki inventory ve transaction log koleksiyonlarını karşılaştırarak tutarsızlıkları tespit etmek ve düzeltmek için kullanılacak ayarları tutmaktır.