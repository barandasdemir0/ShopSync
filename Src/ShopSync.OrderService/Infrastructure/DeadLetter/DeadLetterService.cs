using MongoDB.Driver;
using ShopSync.OrderService.Infrastructure.Persistence;
using ShopSync.OrderService.Models;


namespace ShopSync.OrderService.Infrastructure.DeadLetter;

public sealed class DeadLetterService : IDeadLetterService
{
    private readonly MongoDbContext _context;
    private readonly ILogger<DeadLetterService> _logger;
    public DeadLetterService(MongoDbContext context, ILogger<DeadLetterService> logger)
    {
        _context = context;
        _logger = logger;
    }


    // bu method, bir siparişin işlenmesi sırasında oluşan hataları Dead Letter Queue'ya ekler.
    public async Task EnqueueAsync(Order order, string errorMessage, CancellationToken ct = default)
    {
        var entry = new DeadLetterEntry(order.OrderId, order.Status, errorMessage);

        await _context.DeadLetterQueue.InsertOneAsync(entry, cancellationToken: ct);

        _logger.LogWarning(
            "Sipariş Dead Letter Queue'ya eklendi. OrderId: {OrderId}, Hata: {Error}",
            order.OrderId, errorMessage);
    }


    // bu method, henüz çözülmemiş Dead Letter Queue kayıtlarını getirir.
    public async Task<List<DeadLetterEntry>> GetUnresolvedAsync(CancellationToken ct = default)
    {
        return await _context.DeadLetterQueue
          .Find(x => !x.Resolved)
          .SortByDescending(x => x.FailedAt)
          .ToListAsync(ct);
    }

    // bu method, bir Dead Letter Queue kaydını çözmek için kullanılır. Çözüm bilgisi ile birlikte kaydı günceller.
    public async Task ResolveAsync(string deadLetterId, string resolution, CancellationToken ct = default)
    {
        // Dead Letter kaydını bulmak için filtre oluşturuyoruz.
        var filter = Builders<DeadLetterEntry>.Filter.Eq(x => x.Id, deadLetterId);
        // Dead Letter kaydını veritabanından getiriyoruz.
        var entry = await _context.DeadLetterQueue.Find(filter).FirstOrDefaultAsync(ct);
        if (entry is null)
        {
            _logger.LogWarning("Dead letter kaydı bulunamadı. Id: {Id}", deadLetterId);
            throw new ArgumentException($"Dead letter kaydı bulunamadı: {deadLetterId}");
        }
            
        entry.MarkAsResolved(resolution);
        // Dead Letter kaydını güncelliyoruz.ReplaceOneAsync, mevcut kaydı tamamen değiştirir ve yeni haliyle kaydeder.
        await _context.DeadLetterQueue.ReplaceOneAsync(filter, entry, cancellationToken: ct);
        _logger.LogInformation("Dead letter çözüldü. Id: {Id}, Çözüm: {Resolution}", deadLetterId, resolution);
    }

 
}
