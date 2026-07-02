using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShopSync.InventoryService.Models;

public sealed class InventorySnapshot
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = string.Empty;

    // Snapshot açıklaması
    [BsonElement("description")]
    public string Description { get; private set; } = string.Empty;

    // Snapshot'ın alındığı tarih
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; private set; }

    // Snapshot'taki tüm stok kayıtları
    [BsonElement("items")]
    public List<SnapshotItem> Items { get; private set; } = new();


    // Toplam ürün sayısı (hızlı erişim için)
    [BsonElement("itemCount")]
    public int ItemCount { get; private set; }


    private InventorySnapshot() { }
    public InventorySnapshot(string description, List<SnapshotItem> items)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            Description = "Snapshot";
        }
        else
        {
            Description = description.Trim();
        }

        if (items is null)
        {
            Items = new List<SnapshotItem>();
        }
        else
        {
            Items = items.ToList();
        }
        ItemCount = Items.Count;
        CreatedAt = DateTime.UtcNow;
    }
}
