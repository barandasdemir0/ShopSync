using MongoDB.Bson.Serialization.Attributes;

namespace ShopSync.OrderService.Models;

public sealed class OrderLineItem
{

    [BsonElement("sku")]
    public string Sku { get; private set; } = string.Empty;


    [BsonElement("requestedQuantity")]
    public int RequestedQuantity { get; private set; }


    [BsonElement("reservedQuantity")]
    public int ReservedQuantity { get; private set; }


    private OrderLineItem() { }


    public OrderLineItem(string sku, int requestedQuantity)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU boş olamaz.", nameof(sku));
        }
           
        if (requestedQuantity <= 0)
        {
            throw new ArgumentException("Talep edilen miktar sıfırdan büyük olmalıdır.", nameof(requestedQuantity));
        }
           
        Sku = sku.Trim().ToUpperInvariant();
        RequestedQuantity = requestedQuantity;
        ReservedQuantity = 0;
    }


    // Rezervasyon başarılı olduğunda çağrılır
    // Talep edilen miktar kadar stok rezerve edildi
    public void MarkAsReserved()
    {
        ReservedQuantity = RequestedQuantity;
    }

    // Rezervasyon iptal edildiğinde veya süresi dolduğunda çağrılır
    // Rezerve edilen miktar sıfırlanır
    public void ClearReservation()
    {
        ReservedQuantity = 0;
    }

}
