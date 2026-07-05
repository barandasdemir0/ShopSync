namespace ShopSync.InventoryService.BackgroundJobs;

public sealed class LongHeldLockDetectionJob : BackgroundService
{

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LongHeldLockDetectionJob> _logger;

    // Bu işin ne sıklıkla çalışacağını belirler
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    // Bu süreden uzun tutulan kilitler uyarı üretir
    private readonly TimeSpan _lockWarningThreshold = TimeSpan.FromSeconds(30);

    public LongHeldLockDetectionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<LongHeldLockDetectionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LongHeldLockDetectionJob arka plan görevi başarıyla başlatıldı.");
        _logger.LogInformation("Kontrol aralığı: {Interval} dakika.", _checkInterval.TotalMinutes);
        _logger.LogInformation("Uyarı eşiği: {Threshold} saniye.", _lockWarningThreshold.TotalSeconds);

        // Uygulama ayakta olduğu (kapatılmadığı) sürece döngüyü çalıştır
        while (stoppingToken.IsCancellationRequested == false)
        {
            try
            {
                // Uzun süreli kilitleri tespit etme işini tetikle
                await DetectLongHeldLocksAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Uygulama kapatılıyorsa bu hata normaldir. Döngüden güvenle çıkıyoruz.
                _logger.LogInformation("LongHeldLockDetectionJob iptal edildi (Uygulama kapatılıyor).");
                break;
            }
            catch (Exception ex)
            {
                // İşlem iptali dışındaki tüm gerçek hataları logla
                _logger.LogError(ex, "LongHeldLockDetectionJob çalışırken beklenmeyen bir hata oluştu.");
            }

            // Belirlenen süre kadar bekle ve sonraki tura geç
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }


    private async Task DetectLongHeldLocksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
        var server = redis.GetServers().FirstOrDefault();
        if (server is null)
        {
            return;
        }
        // "lock:" prefix'i ile başlayan tüm anahtarları tara

        var lockKeys = server.Keys(pattern: "lock:*").ToList();

        foreach (var key in lockKeys)
        {
            var db = redis.GetDatabase();
            var ttl = await db.KeyTimeToLiveAsync(key);
            if (ttl.HasValue)
            {
                // Kilidin başlangıç süresini hesapla
                // RedLock kilitleri genellikle 30sn TTL ile oluşturulur.
                // Eğer kalan TTL çok düşükse, kilit uzun süredir tutuluyor demektir.
                var lockAge = TimeSpan.FromSeconds(30) - ttl.Value; // Varsayılan TTL: 30sn
                if (lockAge > _lockWarningThreshold)
                {
                    _logger.LogWarning(
                        "ZUN SÜRELİ KİLİT TESPİT EDİLDİ! " +
                        "Anahtar: {Key}, Tutulan Süre: {Duration}sn, Kalan TTL: {Ttl}sn",
                        key.ToString(),
                        lockAge.TotalSeconds,
                        ttl.Value.TotalSeconds);
                }
            }
            else
            {
                // TTL yok → Kilit sonsuz süreli tutulmuş (tehlikeli!)
                _logger.LogError(
                    "SONSUZ SÜRELİ KİLİT TESPİT EDİLDİ! Anahtar: {Key}. " +
                    "Bu kilit asla serbest bırakılmayacak! Manuel müdahale gerekli.",
                    key.ToString());
            }
        }

    }
}
