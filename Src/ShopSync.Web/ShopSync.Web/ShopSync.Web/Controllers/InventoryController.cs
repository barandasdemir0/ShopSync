using Microsoft.AspNetCore.Mvc;
using ShopSync.Web.Dtos;
using ShopSync.Web.Models;
using ShopSync.Web.Services;

namespace ShopSync.Web.Controllers;

public class InventoryController : Controller
{
    private readonly IShopSyncApi _api;
    public InventoryController(IShopSyncApi api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? sku, int days = 7, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return View();
        try
        {
            // İki isteği aynı anda atıyoruz 
            var stockTask = _api.GetStockAsync(sku, ct);
            var forecastTask = _api.GetForecastAsync(sku, days, ct);
            await Task.WhenAll(stockTask, forecastTask);

            var vm = new InventoryIndexViewModel
            {
                Stock = await stockTask,
                Forecast = await forecastTask,
                Days = days
            };
            return View(vm);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Sorgu hatası: " + (ex.InnerException?.Message ?? ex.Message);
            return View();
        }
    }

    [HttpGet]
    public IActionResult Operations()
    {
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken] // CSRF saldırılarına karşı koruma sağlar
    public async Task<IActionResult> Operations(InventoryOperationRequestDto req, CancellationToken ct)
    {
        try
        {
            var result = req.Operation switch
            {
                // Stok artırma işlemi
                "adjust" when req.Quantity > 0 => await _api.IncreaseStockAsync(new()
                {
                    Sku = req.Sku,
                    Quantity = req.Quantity,
                    Reason = req.Reason!,
                    WarehouseCode = req.WarehouseCode!
                }, ct),
                "adjust" when req.Quantity < 0 => await _api.DecreaseStockAsync(new()
                {
                    Sku = req.Sku,
                    Quantity = Math.Abs(req.Quantity),
                    Reason = req.Reason!,
                    WarehouseCode = req.WarehouseCode!
                }, ct),

                "adjust" => throw new ArgumentException("Stok düzenleme miktarı sıfır olamaz."),

                "rebalance" => await _api.RebalanceStockAsync(new()
                {
                    Sku = req.Sku,
                    Quantity = req.Quantity,
                    FromLocation = req.FromLocation!,
                    ToLocation = req.ToLocation!,
                    Reason = req.Reason!
                }, ct),

                "create-item" => await _api.CreateItemAsync(new()
                {
                    Sku = req.Sku,
                    InitialQuantity = req.InitialQuantity,
                    WarehouseCode = req.WarehouseCode!,
                    LowStockThreshold = req.LowStockThreshold
                }, ct),

                "delete-item" => await _api.DeleteItemAsync(req.WarehouseCode ?? string.Empty, req.Sku, ct),

                _ => throw new ArgumentException("Geçersiz işlem türü.")
            };

            TempData["Success"] = string.IsNullOrWhiteSpace(result.Message)
                ? "İşlem başarıyla tamamlandı."
                : result.Message;
        }
        catch (Exception ex)
        {
            TempData["Error"] = "İşlem hatası: " + (ex.InnerException?.Message ?? ex.Message);
        }
        return RedirectToAction(nameof(Operations));
    }

    [HttpGet]
    public IActionResult Snapshot()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Snapshot(string action, string description, string snapshotId, CancellationToken ct)
    {
        try
        {
            StockOperationResponseDto result;
            if (action == "create")
            {
                result = await _api.CreateSnapshotAsync(new CreateSnapshotDto 
                { 
                    Description = description 
                }, ct);
                TempData["Success"] = "Snapshot başarıyla oluşturuldu.";
            }
            else
            {
                result = await _api.RestoreSnapshotAsync(snapshotId, ct);
                TempData["Success"] = "Snapshot başarıyla geri yüklendi.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Snapshot hatası: " + (ex.InnerException?.Message ?? ex.Message);
        }
        return RedirectToAction(nameof(Snapshot));
    }
    [HttpGet]
    public async Task<IActionResult> Forecast(string? sku, int days = 7, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return View();

        } 
        try
        {
            var result = await _api.GetForecastAsync(sku, days, ct);
            return View(result);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Tahmin hatası: " + (ex.InnerException?.Message ?? ex.Message);
            return View();
        }
    }
}
