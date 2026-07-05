using ShopSync.OrderService.Models;

namespace ShopSync.OrderService.Infrastructure.DeadLetter;

public interface IDeadLetterService
{
    // DeadLetter kuyruğuna sipariş ekler
    Task EnqueueAsync(Order order, string errorMessage, CancellationToken ct = default);

    // DeadLetter kuyruğundaki çözülmemiş siparişleri döndürür
    Task<List<DeadLetterEntry>> GetUnresolvedAsync(CancellationToken ct = default);

    // DeadLetter kuyruğundaki çözülmemiş siparişleri döndürür
    Task ResolveAsync(string deadLetterId, string resolution, CancellationToken ct = default);
}
