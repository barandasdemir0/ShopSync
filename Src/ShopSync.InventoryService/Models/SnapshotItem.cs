using MongoDB.Bson.Serialization.Attributes;

namespace ShopSync.InventoryService.Models;

public sealed class SnapshotItem
{

    // Stok kaydının benzersiz kimliği
    [BsonElement("sku")]
    public string Sku { get; set; } = string.Empty;

    // Stok kaydının ait olduğu depo kodu
    [BsonElement("warehouseCode")]
    public string WarehouseCode { get; set; } = string.Empty;

    // Stok kaydının mevcut miktarı
    [BsonElement("quantityAvailable")]
    public int QuantityAvailable { get; set; }

    // Stok kaydının rezerve edilmiş miktarı
    [BsonElement("quantityReserved")]
    public int QuantityReserved { get; set; }

    // Stok kaydının düşük stok eşiği
    [BsonElement("lowStockThreshold")]
    public int LowStockThreshold { get; set; }
}
