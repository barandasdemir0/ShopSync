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


    // CountByStatusAsync metodu, belirli bir durumdaki siparişlerin sayısını döndürür.
    public async Task<long> CountByStatusAsync(string status, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var filterBuilder = Builders<Order>.Filter; // MongoDB filtre oluşturucu
        var filters = new List<FilterDefinition<Order>> // Filtreleri biriktireceğimiz liste filterdefinition tipinde olacak çünkü MongoDB filtreleri bu tipte tanımlanır
            {
                filterBuilder.Eq(x => x.Status, status)
            };

        if (from.HasValue) // Eğer from parametresi verilmişse, CreatedAt alanı from tarihinden büyük veya eşit olanları filtrele
        {
            filters.Add(filterBuilder.Gte(x => x.CreatedAt, from.Value)); // gte = greater than or equal yani from tarihinden büyük veya eşit olanlar
        }
        if (to.HasValue) // Eğer to parametresi verilmişse, CreatedAt alanı to tarihinden küçük veya eşit olanları filtrele
        {
            filters.Add(filterBuilder.Lte(x => x.CreatedAt, to.Value)); // lte = less than or equal yani to tarihinden küçük veya eşit olanlar
        }
        return await _context.Orders.CountDocumentsAsync(filterBuilder.And(filters), cancellationToken: ct); // Tüm filtreleri "VE" (AND) koşuluyla birleştirip sayısını döndür countdocumentsasync metodu ise MongoDB'de filtrelenmiş belgelerin sayısını döndürür
    }

    public async Task<List<Order>> GetAllOrdersForAnalyticsAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var filterBuilder = Builders<Order>.Filter;
        var filters = new List<FilterDefinition<Order>>();
        // Sadece tarih aralığı filtrelerini ekliyoruz
        if (from.HasValue)
        {
            filters.Add(filterBuilder.Gte(x => x.CreatedAt, from.Value));
        }

        if (to.HasValue)
        {
            filters.Add(filterBuilder.Lte(x => x.CreatedAt, to.Value));
        }

        // 1. Önce varsayılan filtreyi belirliyoruz: "Filtresiz (Tümünü getir)"
        var combinedFilter = filterBuilder.Empty;

        // 2. Eğer yukarıda listeye filtre eklendiyse, bunları "VE (AND)" koşuluyla birleştiriyoruz
        if (filters.Count > 0)
        {
            combinedFilter = filterBuilder.And(filters); // Tüm filtreleri birleştirip tek bir filtre haline getiriyoruz
        }


        // Siparişleri bul
        return await _context.Orders.Find(combinedFilter).ToListAsync(ct);
    }


    // GetAverageTransitionTimeAsync metodu, belirli bir duruma geçiş süresinin ortalamasını döndürür.
    public async Task<double> GetAverageTransitionTimeAsync(string targetStatus, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var filterBuilder = Builders<Order>.Filter;
        var filters = new List<FilterDefinition<Order>>
             {
        filterBuilder.Eq(x => x.Status, targetStatus)
            };

        if (from.HasValue)
        {
            filters.Add(filterBuilder.Gte(x => x.CreatedAt, from.Value));
        }

        if (to.HasValue)
        {
            filters.Add(filterBuilder.Lte(x => x.CreatedAt, to.Value));
        }

        var orders = await _context.Orders
            .Find(filterBuilder.And(filters))
            .Limit(1000)  // Son 1000 sipariş ile hesapla
            .ToListAsync(ct);


        if (orders.Count == 0)
        {
            return 0;
        }
        // Toplam geçen süreyi ve geçerli sipariş sayısını tutuyoruz
        double totalSecondsSum = 0;
        int validOrderCount = 0;

        foreach (var order in orders)
        {
            // 1. Siparişin geçmiş kayıtları arasında, aradığımız duruma ait en son geçişi bul
            var transition = order.History.LastOrDefault(h => h.Status.ToString() == targetStatus);

            // 2. Eğer sipariş bu duruma gerçekten geçmişse hesaplamaya dahil et
            if (transition != null)
            {
                // 3. Duruma geçiş zamanından, siparişin ilk oluşturulma zamanını çıkarıp saniye cinsini al
                double secondsTaken = (transition.Timestamp - order.CreatedAt).TotalSeconds;

                // 4. Bazen sistem senkronizasyonundan dolayı 0 veya eksi değerler oluşabilir, 
                // sadece mantıklı (0'dan büyük) süreleri topla
                if (secondsTaken > 0)
                {
                    totalSecondsSum += secondsTaken;
                    validOrderCount++;
                }
            }
        }
        // 5. Eğer listede bu duruma geçen mantıklı süreli hiçbir sipariş yoksa 0 dön
        if (validOrderCount == 0)
        {
            return 0;
        }
        // 6. Toplam saniyeyi, sipariş sayısına bölerek ortalamayı bul
        double averageSeconds = totalSecondsSum / validOrderCount;
        // 7. Sonucu virgülden sonra 2 hane olacak şekilde yuvarla ve dön
        return Math.Round(averageSeconds, 2);
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
