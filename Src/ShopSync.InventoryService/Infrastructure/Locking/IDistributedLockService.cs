namespace ShopSync.InventoryService.Infrastructure.Locking;

public interface IDistributedLockService
{
    //buranın amacı, birden fazla instance'ın aynı anda aynı kaynağa erişmesini engellemek için dağıtık bir kilit mekanizması sağlamaktır.

    //keys Kilitlenecek anahtarlar (genellikle SKU kodları)
    //expiry Kilit süresi (opsiyonel, varsayılan olarak 30 saniye)
    Task<IAsyncDisposable> AcquireLocksAsync(IEnumerable<string> keys,TimeSpan? expiry = null,CancellationToken cancellationToken = default);
}
