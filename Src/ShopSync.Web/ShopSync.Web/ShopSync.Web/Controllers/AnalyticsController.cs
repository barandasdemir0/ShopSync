using Microsoft.AspNetCore.Mvc;
using ShopSync.Web.Dtos;
using ShopSync.Web.Models;
using ShopSync.Web.Services;

namespace ShopSync.Web.Controllers;

public class AnalyticsController : Controller
{
    private readonly IShopSyncApi _api;
    public AnalyticsController(IShopSyncApi api)
    {
        _api = api;
    }
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken ct)
    {
        try
        {
            var result = await _api.GetAnalyticsAsync(from, to, ct);
            var vm = new AnalyticsViewModel 
            {
                Data = result 
            };
            return View(vm);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Analitik yüklenemedi: " + (ex.InnerException?.Message ?? ex.Message);
            return View(new AnalyticsViewModel()); // Boş model dönüyoruz
        }
    }
}
