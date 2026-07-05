using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShopSync.OrderService.Models;

//DeadLetterEntry - Başarısız İşlem Kaydı
public sealed class DeadLetterEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = string.Empty;

    // Sorunlu siparişin OrderId'si
    [BsonElement("orderId")]
    public string OrderId { get; private set; } = string.Empty;


    // Siparişin son bilinen durumu
    [BsonElement("lastKnownStatus")]
    public string LastKnownStatus { get; private set; } = string.Empty;


    // Hata mesajı
    [BsonElement("errorMessage")]
    public string ErrorMessage { get; private set; } = string.Empty;


    // Hatanın oluştuğu zaman
    [BsonElement("failedAt")]
    public DateTime FailedAt { get; private set; }


    // Kaç kez denendiği
    [BsonElement("retryCount")]
    public int RetryCount { get; private set; }


    // Manuel olarak çözüldü mü?
    [BsonElement("resolved")]
    public bool Resolved { get; private set; }


    // Çözüm notu
    [BsonElement("resolution")]
    public string? Resolution { get; private set; }



    private DeadLetterEntry() { }


    public DeadLetterEntry(string orderId, string lastKnownStatus, string errorMessage)
    {
        OrderId = orderId;
        LastKnownStatus = lastKnownStatus;
        ErrorMessage = errorMessage;
        FailedAt = DateTime.UtcNow;
        RetryCount = 1;
        Resolved = false;
    }


    // Admin tarafından çözüldü olarak işaretle
    public void MarkAsResolved(string resolution)
    {
        Resolved = true;
        Resolution = resolution;
    }

    // Retry sayısını artır
    public void IncrementRetry()
    {
        RetryCount++;
    }


}
