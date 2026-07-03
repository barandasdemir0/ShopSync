using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShopSync.InventoryService.Models;

public sealed class InventoryTransactionLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = string.Empty;


    
    [BsonElement("sku")]
    public string Sku { get; private set; } = string.Empty; // Ürün stok kodunu temsil eder.


    [BsonElement("transactionType")]
    public string TransactionType { get; private set; } = string.Empty; // İşlem türünü temsil eder. Örn: "RESERVE", "RELEASE", "INCREASE", "DECREASE", "REBALANCE"

    [BsonIgnore]
    public InventoryTransactionType Type => InventoryTransactionType.FromCode(TransactionType); // TransactionType kodunu kullanarak ilgili InventoryTransactionType nesnesini döndürür. bunu böyle yapmamın sebebi, TransactionType alanını string olarak saklamak ve aynı zamanda InventoryTransactionType nesnesine erişim sağlamaktır.



    [BsonElement("quantity")]
    public int Quantity { get; private set; } // İşlem miktarını temsil eder.

    [BsonElement("orderId")]
    public string? OrderId { get; private set; } // İlgili siparişin kimliğini temsil eder. Eğer işlem bir siparişle ilişkili değilse null olabilir.

    [BsonElement("previousAvailable")]
    public int PreviousAvailable { get; private set; } // İşlem öncesi mevcut stok miktarını temsil eder.


    [BsonElement("newAvailable")]
    public int NewAvailable { get; private set; } // İşlem sonrası mevcut stok miktarını temsil eder.


    [BsonElement("previousReserved")]
    public int PreviousReserved { get; private set; } // İşlem öncesi rezerve edilmiş stok miktarını temsil eder. 


    [BsonElement("newReserved")]
    public int NewReserved { get; private set; } // İşlem sonrası rezerve edilmiş stok miktarını temsil eder.


    [BsonElement("reason")]
    public string? Reason { get; private set; } // İşlemin nedenini temsil eder. Örn: "Customer order", "Stock adjustment", vb. Eğer işlem bir nedenle ilişkili değilse null olabilir.

    [BsonElement("correlationId")]
    public string? CorrelationId { get; private set; } // İşlemin izlenebilirliğini sağlamak için kullanılan bir kimliktir. Eğer işlem bir correlationId ile ilişkili değilse null olabilir.


    [BsonElement("timestamp")]
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow; // İşlem zaman damgasını temsil eder.


    [BsonConstructor]
    public InventoryTransactionLog()
    {

    }

    public InventoryTransactionLog(string sku,
        InventoryTransactionType transactionType,
        int quantity,
        int previousAvailable,
        int newAvailable,
        int previousReserved,
        int newReserved,
        string? orderId = null,
        string? reason = null,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU alanı boş bırakılamaz.", nameof(sku));
        }

        if (transactionType is null)
        {
            throw new ArgumentNullException(nameof(transactionType));
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Miktar sıfırdan büyük olmalıdır.", nameof(quantity));
        }

        if (previousAvailable < 0)
        {
            throw new ArgumentException("Önceki mevcut miktar negatif olamaz.", nameof(previousAvailable));
        }

        if (newAvailable < 0)
        {
            throw new ArgumentException("Yeni kullanılabilir miktar negatif olamaz.", nameof(newAvailable));
        }

        if (previousReserved < 0)
        {
            throw new ArgumentException("Önceden rezerve edilen miktar negatif olamaz.", nameof(previousReserved));
        }

        if (newReserved < 0)
        {
            throw new ArgumentException("Yeni ayrılan miktar negatif olamaz.", nameof(newReserved));
        }

        Sku = sku.Trim().ToUpperInvariant();
        TransactionType = transactionType.Code;
        Quantity = quantity;
        OrderId = orderId;
        PreviousAvailable = previousAvailable;
        NewAvailable = newAvailable;
        PreviousReserved = previousReserved;
        NewReserved = newReserved;
        Reason = reason;
        CorrelationId = correlationId;
        Timestamp = DateTime.UtcNow;

    }



}
