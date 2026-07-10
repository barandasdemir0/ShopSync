using Microsoft.AspNetCore.Mvc;
using ShopSync.Web.Dtos;
using ShopSync.Web.Services;

namespace ShopSync.Web.Controllers;

public class HomeController : Controller
{
    private readonly IShopSyncApi _api;
    public HomeController(IShopSyncApi api)
    {
        _api = api;
    }
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var analytics = await _api.GetAnalyticsAsync(null, null, ct);
            return View(analytics);
        }
        catch
        {
            return View(new OrderAnalyticsResponseDto());
        }
    }
}
