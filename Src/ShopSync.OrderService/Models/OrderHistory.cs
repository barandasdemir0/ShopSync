using MongoDB.Bson.Serialization.Attributes;

namespace ShopSync.OrderService.Models;

public sealed class OrderHistory
{

    // Hangi duruma geçildi (Örn: "CONFIRMED")
    [BsonElement("status")]
    public string Status { get; private set; } = string.Empty;

    // Durum geçişinin gerçekleştiği zaman
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; private set; }

    // Durum değişikliğinin sebebi veya ek not
    [BsonElement("reason")]
    public string? Reason { get; private set; }


    // MongoDB deserialization için gerekli
    private OrderHistory() { }

    public OrderHistory(OrderStatus status, string? reason = null)
    {
        Status = status.Code;
        Timestamp = DateTime.UtcNow;
        Reason = reason;
    }

}
