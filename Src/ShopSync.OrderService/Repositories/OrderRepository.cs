using MongoDB.Driver;
using ShopSync.OrderService.Infrastructure.Persistence;
using ShopSync.OrderService.Models;

namespace ShopSync.OrderService.Repositories;

public sealed class OrderRepository : IOrderRepository
{

    private readonly MongoDbContext _context;
    public OrderRepository(MongoDbContext context)
    {
        _context = context;
    }
    public async Task<Order?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.Orders
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }
            
        return await _context.Orders
            .Find(x => x.IdempotencyKey == idempotencyKey.Trim())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Order?> GetByOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        var normalizedId = orderId.Trim();
        return await _context.Orders
            .Find(x => x.OrderId == normalizedId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Order>> GetByOrderIdsAsync(IEnumerable<string> orderIds, CancellationToken ct = default)
    {
        var normalizedIds = orderIds.Select(id => id.Trim()).ToList();
        var filter = Builders<Order>.Filter.In(x => x.OrderId, normalizedIds);
        return await _context.Orders.Find(filter).ToListAsync(ct);

    }

    // Süresi dolmuş siparişler, halen BEKLEMEDE durumunda olan ve belirtilen tarihten önce oluşturulmuş siparişlerdir.
    public async Task<List<Order>> GetExpiredOrdersAsync(DateTime olderThan, CancellationToken ct = default)
    {
        // Status enum/string koda gömüldü çünkü Expiration Job daima PENDING olanları arar.
        var filter = Builders<Order>.Filter.And(
            Builders<Order>.Filter.Eq(x => x.Status, OrderStatus.Pending.Code),
            Builders<Order>.Filter.Lt(x => x.CreatedAt, olderThan)); // lt = less than yani önce en eski siparişler
        return await _context.Orders.Find(filter).ToListAsync(ct);
    }

    public async Task<List<Order>> GetOrdersByStatusBeforeDateAsync(string status, DateTime cutoffTime, CancellationToken ct = default)
    {
        var filter = Builders<Order>.Filter.And(
       Builders<Order>.Filter.Eq(x => x.Status, status),
       Builders<Order>.Filter.Lt(x => x.CreatedAt, cutoffTime));

        return await _context.Orders.Find(filter).ToListAsync(ct);
    }

    public async Task InsertAsync(Order order, CancellationToken ct = default)
    {
        if (order is null)
        {
            throw new ArgumentNullException(nameof(order));
        }
            
        await _context.Orders.InsertOneAsync(order, cancellationToken: ct);
    }

    public async Task<List<Order>> ListOrdersAsync(OrderFilter filter, CancellationToken ct = default)
    {
        if (filter is null)
        {
            filter = new OrderFilter(); // Parametre boş gelirse varsayılanı kullan
        }
           
        var filterBuilder = Builders<Order>.Filter;
        var filters = new List<FilterDefinition<Order>>(); // Filtreleri biriktireceğimiz liste
        // Status filtresi
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(filterBuilder.Eq(x => x.Status, filter.Status.Trim().ToUpperInvariant()));
        }
        // From (Başlangıç tarihi) filtresi
        if (filter.From.HasValue)
        {
            filters.Add(filterBuilder.Gte(x => x.CreatedAt, filter.From.Value));
        }
        // To (Bitiş tarihi) filtresi
        if (filter.To.HasValue)
        {
            filters.Add(filterBuilder.Lte(x => x.CreatedAt, filter.To.Value));
        }
        // 1. Önce varsayılan durumu belirliyoruz: "Filtre yok (Empty)"
        var combinedFilter = filterBuilder.Empty;

        // 2. Sonra listemizde geçerli filtreler var mı diye kontrol ediyoruz
        if (filters.Count > 0)
        {
            // Eğer filtrelerimiz varsa, boş filtreyi ezip yerine
            // tüm filtreleri "VE" (AND) koşuluyla birleştiren asıl filtremizi koyuyoruz.
            combinedFilter = filterBuilder.And(filters);
        }

        return await _context.Orders
            .Find(combinedFilter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Limit(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        if (order is null)
        {
            throw new ArgumentNullException(nameof(order));
        }
          
        var filter = Builders<Order>.Filter.Eq(x => x.Id, order.Id);
        await _context.Orders.ReplaceOneAsync(filter, order, cancellationToken: ct);
    }
}
