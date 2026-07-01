using Ardalis.SmartEnum;


namespace ShopSync.InventoryService.Models;

public sealed class InventoryTransactionType : SmartEnum<InventoryTransactionType>
{
    // İşlem türlerini temsil eden sabitler
    public static readonly InventoryTransactionType Reserve = new(nameof(Reserve), 1, "RESERVE", "Stok rezervasyonu");// 

    public static readonly InventoryTransactionType Release = new(nameof(Release), 2, "RELEASE", "Rezervasyon serbest bırakma");

    public static readonly InventoryTransactionType Increase = new(nameof(Increase), 3, "INCREASE", "Stok artırma");


    public static readonly InventoryTransactionType Decrease = new(nameof(Decrease), 4, "DECREASE", "Stok azaltma");

    public static readonly InventoryTransactionType Rebalance = new(nameof(Rebalance), 5, "REBALANCE", "Stok yeniden dengeleme");

    public static readonly InventoryTransactionType Confirm = new(nameof(Confirm), 6, "CONFIRM", "Rezervasyon onaylama");

    public static readonly InventoryTransactionType Expiration = new(nameof(Expiration), 7, "EXPIRATION", "Süresi dolmuş rezervasyon serbest bırakma");

    public string Code { get; }
    public string Description { get; }

    private InventoryTransactionType(string name, int value,string code,string description) : base(name, value)
    {
        Code = code;
        Description = description;
    }

    // Bu metot, verilen kodu kullanarak ilgili InventoryTransactionType nesnesini döndürür.
    public static InventoryTransactionType FromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("İşlem türü kodu boş bırakılamaz.", nameof(code));
        }


        var transactionType = List.FirstOrDefault(x =>
            string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

        if (transactionType is null)
        {
            throw new ArgumentException($"Geçersiz işlem türü kodu: {code}", nameof(code));
        }

        return transactionType;


    }
}


