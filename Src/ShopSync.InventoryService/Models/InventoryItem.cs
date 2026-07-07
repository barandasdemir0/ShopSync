using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ShopSync.InventoryService.Exceptions;

namespace ShopSync.InventoryService.Models;

public sealed class InventoryItem
{
    //magic stringleri önlemek için default değerleri sabit olarak tanımladım.
    private const string DefaultWarehouseCode = "DEFAULT";
    private const int DefaultLowStockThreshold = 10;


    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = string.Empty; // MongoDB'deki "_id" alanını temsil eder.


    [BsonElement("sku")]
    public string Sku { get; private set; } = string.Empty; // Ürün stok kodunu temsil eder.

    [BsonElement("quantityAvailable")]
    public int QuantityAvailable { get; private set; } // Mevcut stok miktarını temsil eder.

    [BsonElement("quantityReserved")]
    public int QuantityReserved { get; private set; }// Rezerve edilmiş stok miktarını temsil eder.


    [BsonElement("warehouseCode")]
    public string WarehouseCode { get; private set; } = DefaultWarehouseCode; // Depo kodunu temsil eder. Varsayılan olarak "DEFAULT" olarak ayarlanmıştır.

    [BsonElement("lowStockThreshold")]
    public int LowStockThreshold { get; private set; } = DefaultLowStockThreshold; // Düşük stok eşiğini temsil eder. Varsayılan olarak 10 olarak ayarlanmıştır. bu sayının altına düşerse alert gönderilecek.


    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow; // Oluşturulma tarihini temsil eder. Varsayılan olarak UTC zaman diliminde şimdiki tarih ve saat olarak ayarlanmıştır.

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow; // Güncellenme tarihini temsil eder. Varsayılan olarak UTC zaman diliminde şimdiki tarih ve saat olarak ayarlanmıştır.




    private InventoryItem()
    {
    }

    public InventoryItem(
       string sku,
       int quantityAvailable,
       string warehouseCode = DefaultWarehouseCode,
       int lowStockThreshold = DefaultLowStockThreshold)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU cannot be empty.", nameof(sku));
        }


        if (quantityAvailable < 0)
        {
            throw new ArgumentException("Quantity available cannot be negative.", nameof(quantityAvailable));
        }

        if (string.IsNullOrWhiteSpace(warehouseCode))
        {
            throw new ArgumentException("Warehouse code cannot be empty.", nameof(warehouseCode));
        }


        if (lowStockThreshold < 0)
        {
            throw new ArgumentException("Low stock threshold cannot be negative.", nameof(lowStockThreshold));
        }


        Sku = sku.Trim().ToUpperInvariant(); // SKU'yu büyük harfe çevirir ve boşlukları kaldırır.
        QuantityAvailable = quantityAvailable;
        QuantityReserved = 0;
        WarehouseCode = warehouseCode.Trim().ToUpperInvariant();
        LowStockThreshold = lowStockThreshold;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // Bu metot, belirtilen miktarda stok rezervasyonu yapılıp yapılamayacağını kontrol eder.
    public bool CanReserve(int quantity)
    {
        // Miktar sıfırdan büyükse ve stokta yeterince varsa rezervasyon yapılabilir.
        if (quantity > 0)
        {
            if (QuantityAvailable >= quantity)
            {
                return true; // Hem sıfırdan büyük hem de stokta yeterince var
            }
            else
            {
                return false; // Sıfırdan büyük ama stokta o kadar yok
            }
        }
        else
        {
            return false; // Miktar sıfır veya daha küçük
        }
    }

    public bool CanRelease(int quantity)
    {
        // Miktar sıfırdan büyükse ve rezerve edilmiş stokta yeterince varsa serbest bırakılabilir.
        if (quantity > 0)
        {
            if (QuantityReserved >= quantity)
            {
                return true; // Hem sıfırdan büyük hem de rezerve edilmiş stokta yeterince var
            }
            else
            {
                return false; // Sıfırdan büyük ama rezerve edilmiş stokta o kadar yok
            }
        }
        else
        {
            return false; // Miktar sıfır veya daha küçük
        }
    }

    // Bu metot, belirtilen miktarda stok rezervasyonu yapar.
    public void Reserve(int quantity)
    {
        // Miktarın sıfırdan büyük olup olmadığını kontrol eder.
        if (quantity <= 0)
        {
            throw new ArgumentException("Rezerv miktarı sıfırdan büyük olmalıdır.", nameof(quantity));
        }
            

        if (QuantityAvailable < quantity)
        {
            throw new DomainException( "Rezervasyon için yeterli stok bulunmamaktadır.","INSUFFICIENT_STOCK");
        }

        QuantityAvailable -= quantity;
        QuantityReserved += quantity;

        Touch();
    }

    // Bu metot, belirtilen miktarda rezerve edilmiş stok serbest bırakır.
    public void Release(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Serbest bırakılacak miktar sıfırdan büyük olmalıdır.", nameof(quantity));
        }



        if (QuantityReserved < quantity)
        {
            throw new DomainException("Rezerve edilen miktar, serbest bırakılmaya yetmiyor.","RESERVED_QUANTITY_NOT_ENOUGH");
        }
            

        QuantityReserved -= quantity;
        QuantityAvailable += quantity;

        Touch();
    }


    // Bu metot, belirtilen miktarda rezerve edilmiş stok onaylar ve rezerve edilen miktarı azaltır.
    public void ConfirmReservation(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Onaylanacak miktar sıfırdan büyük olmalıdır.", nameof(quantity));
        }
            

        if (QuantityReserved < quantity)
        {
            throw new DomainException( "Rezervasyon miktarı onaylamak için yeterli değil.", "RESERVATION_CONFIRM_QUANTITY_NOT_ENOUGH");
        }

        QuantityReserved -= quantity;

        Touch();
    }


    // Bu metot, belirtilen miktarda stok artırır.
    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Artış miktarı sıfırdan büyük olmalıdır.", nameof(quantity));
        }

        QuantityAvailable += quantity;

        Touch();
    }


    // Bu metot, belirtilen miktarda stok azaltır.
    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Azalma miktarı sıfırdan büyük olmalıdır.", nameof(quantity));
        }

        if (QuantityAvailable < quantity)
        {
            throw new DomainException("Mevcut stok, azaltma işlemi için yeterli değil.", "AVAILABLE_STOCK_NOT_ENOUGH");
        }

        QuantityAvailable -= quantity;

        Touch();
    }

    // Bu metot, düşük stok eşiğini değiştirir.
    public void ChangeLowStockThreshold(int threshold)
    {
        if (threshold < 0)
        {
            throw new ArgumentException("Düşük stok eşiği negatif olamaz.", nameof(threshold));
        }
           

        LowStockThreshold = threshold;

        Touch();
    }

    // Bu metot, mevcut stok miktarının düşük stok eşiğinin altında olup olmadığını kontrol eder.
    public bool IsLowStock()
    {
        // Eğer mevcut stok miktarı düşük stok eşiğine eşit veya daha az ise, düşük stok durumu true döner.
        if (QuantityAvailable <= LowStockThreshold)
        {
            return true; // Stok, düşük stok sınırına eşit veya daha az
        }
        else
        {
            return false; // Stok henüz kritik seviyeye düşmemiş
        }
    }

    //  Bu metot, snapshot geri yükleme işleminde stokların ve rezerve miktarların birebir ezilmesini sağlar.
    public void RestoreSnapshotState(int quantityAvailable, int quantityReserved)
    {
        if (quantityAvailable < 0)
        {
            throw new ArgumentException("Geri yüklenen mevcut stok negatif olamaz.", nameof(quantityAvailable));
        }
            

        if (quantityReserved < 0)
        {
            throw new ArgumentException("Geri yüklenen rezerve stok negatif olamaz.", nameof(quantityReserved));
        }

        QuantityAvailable = quantityAvailable;
        QuantityReserved = quantityReserved;
        Touch();
    }

    // Bu metot, güncellenme tarihini günceller.
    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }

}



// BsonId: Bu alanın MongoDB'deki "_id" alanı olduğunu belirtir.
// BsonRepresentation: ObjectId tipini string olarak kullanmamıza izin verir. 

//ArgumentException = metoda verilen parametre hatalı
//DomainException   = parametre mantıklı ama iş kuralı izin vermiyor