using Ardalis.SmartEnum;

namespace ShopSync.OrderService.Models;

public sealed class OrderStatus : SmartEnum<OrderStatus>
{
    // Sipariş oluşturuldu, stok rezerve edildi, onay bekleniyor
    public static readonly OrderStatus Pending = new(nameof(Pending), 1, "PENDING", "Onay bekliyor");

    // Müşteri siparişi onayladı, stok kesinleşti
    public static readonly OrderStatus Confirmed = new(nameof(Confirmed), 2, "CONFIRMED", "Onaylandı");

    // Sipariş iptal edildi, stoklar serbest bırakıldı
    public static readonly OrderStatus Cancelled = new(nameof(Cancelled), 3, "CANCELLED", "İptal edildi");

    // 10 dakika içinde onaylanmadı, stoklar otomatik serbest bırakıldı
    public static readonly OrderStatus Expired = new(nameof(Expired), 4, "EXPIRED", "Süresi doldu");


    // MongoDB'de saklanan string değer (Örn: "PENDING")
    public string Code { get; }

    // Kullanıcı dostu açıklama
    public string Description { get; }

    private OrderStatus(string name, int value, string code, string description)
      : base(name, value)
    {
        Code = code;
        Description = description;
    }


    // MongoDB'den okunan string kodu OrderStatus nesnesine dönüştürür.
    // Örnek: "PENDING" → OrderStatus.Pending
    public static OrderStatus FromCode(string code)
    {
        var result = List.FirstOrDefault(s => s.Code == code);
        if (result is null)
        {
            throw new ArgumentException($"Geçersiz sipariş durumu kodu: {code}");
        }
            
        return result;
    }
}
