using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ShopSync.OrderService.Exceptions;

namespace ShopSync.OrderService.Models;

public sealed class Order
{

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = string.Empty;


    // Sipariş için benzersiz kimlik (client tarafından sağlanabilir)
    // Idempotency kontrolü için kullanılır
    [BsonElement("orderId")]
    public string OrderId { get; private set; } = string.Empty;


    // Siparişin mevcut durumu (MongoDB'de string olarak saklanır)
    // Örn: "PENDING", "CONFIRMED", "CANCELLED", "EXPIRED"
    [BsonElement("status")]
    public string Status { get; private set; } = string.Empty;


    // SmartEnum nesnesine dönüştürülmüş hali (MongoDB'ye yazılmaz)
    [BsonIgnore]
    public OrderStatus CurrentStatus => OrderStatus.FromCode(Status);


    // Siparişteki ürün kalemleri (embedded list)
    [BsonElement("lineItems")]
    public List<OrderLineItem> LineItems { get; private set; } = new();


    // Siparişin tüm durum geçişlerinin kaydı (audit trail)
    [BsonElement("history")]
    public List<OrderHistory> History { get; private set; } = new();


    // Idempotency key - Aynı siparişin iki kez oluşmasını engeller
    [BsonElement("idempotencyKey")]
    public string? IdempotencyKey { get; private set; }


    // Siparişin oluşturulma tarihi
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; private set; }


    // Son güncelleme tarihi
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; private set; }


    // MongoDB deserialization için gerekli
    private Order() { }
    // Yeni sipariş oluşturmak için kullanılan constructor
    public Order(string orderId, List<OrderLineItem> lineItems, string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Sipariş ID'si boş olamaz.", nameof(orderId));
        }
            
        if (lineItems is null || lineItems.Count == 0)
        {
            throw new ArgumentException("Sipariş en az bir ürün kalemi içermelidir.", nameof(lineItems));
        }
            
        OrderId = orderId;
        LineItems = lineItems;
        IdempotencyKey = idempotencyKey;
        Status = OrderStatus.Pending.Code;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        // İlk durum geçişini kaydet
        History.Add(new OrderHistory(OrderStatus.Pending, "Sipariş oluşturuldu"));
    }


    // Rezervasyon başarılı olduğunda tüm satır kalemlerini işaretle
    public void MarkItemsAsReserved()
    {
        foreach (var item in LineItems)
        {
            item.MarkAsReserved();
        }
        Touch();
    }

    // Siparişi onayla: Pending → Confirmed
    // Sadece Pending durumundaki siparişler onaylanabilir
    public void Confirm()
    {
        if (CurrentStatus != OrderStatus.Pending)
        {
            throw new DomainException(
               $"Sadece bekleyen siparişler onaylanabilir. Mevcut durum: {Status}",
               "ORDER_INVALID_STATE_TRANSITION");
        }
           
        Status = OrderStatus.Confirmed.Code;
        History.Add(new OrderHistory(OrderStatus.Confirmed, "Sipariş onaylandı"));
        Touch();
    }

    // Siparişi iptal et: Pending → Cancelled
    // Sadece Pending durumundaki siparişler iptal edilebilir
    public void Cancel(string? reason = null)
    {
        if (CurrentStatus != OrderStatus.Pending)
        {
            throw new DomainException(
               $"Sadece bekleyen siparişler iptal edilebilir. Mevcut durum: {Status}",
               "ORDER_INVALID_STATE_TRANSITION");
        }
           
        Status = OrderStatus.Cancelled.Code;
        foreach (var item in LineItems)
        {
            item.ClearReservation();
        }

        // 1. İptal sebebini belirle (Klasik null kontrolü)
        string cancellationReason;

        if (reason == null)
        {
            // Dışarıdan bir sebep gönderilmediyse varsayılan metni kullan
            cancellationReason = "Sipariş iptal edildi";
        }
        else
        {
            // Dışarıdan bir sebep gönderildiyse onu kullan
            cancellationReason = reason;
        }

        // 2. Yeni sipariş geçmişi nesnesini oluştur
        var historyRecord = new OrderHistory(OrderStatus.Cancelled, cancellationReason);

        // 3. Oluşturulan bu temiz nesneyi geçmiş listesine (History) ekle
        History.Add(historyRecord);
        Touch();
    }

    // Siparişin süresini doldur: Pending → Expired
    // Background job tarafından çağrılır
    public void Expire()
    {
        if (CurrentStatus != OrderStatus.Pending)
        {
            throw new DomainException(
               $"Sadece bekleyen siparişlerin süresi dolabilir. Mevcut durum: {Status}",
               "ORDER_INVALID_STATE_TRANSITION");
        }
           
        Status = OrderStatus.Expired.Code;
        foreach (var item in LineItems)
        {
            item.ClearReservation();
        }
        History.Add(new OrderHistory(OrderStatus.Expired, "Rezervasyon süresi doldu (10 dk)"));
        Touch();
    }

    // Admin Override: Expired → Cancelled
    // Admin tarafından expire olmuş siparişi iptal etmek için kullanılır
    // Normal kullanıcı bu geçişi yapamaz
    public void AdminOverrideCancel(string reason)
    {
        if (CurrentStatus != OrderStatus.Expired)
        {
            throw new DomainException(
               $"Admin override sadece süresi dolmuş siparişlerde kullanılabilir. Mevcut durum: {Status}",
               "ORDER_INVALID_STATE_TRANSITION");
        }
           
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Admin override için bir sebep belirtilmelidir.", nameof(reason));
        }
          
        Status = OrderStatus.Cancelled.Code;
        History.Add(new OrderHistory(OrderStatus.Cancelled, $"[ADMIN OVERRIDE] {reason}"));
        Touch();
    }

    // Son güncelleme zamanını güncelle
    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
