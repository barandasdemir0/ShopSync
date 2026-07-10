using Microsoft.AspNetCore.Mvc;
using ShopSync.Web.Dtos;
using ShopSync.Web.Services;

namespace ShopSync.Web.Controllers;

public class OrderController : Controller
{
    private readonly IShopSyncApi _api;
    public OrderController(IShopSyncApi api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index([FromQuery] OrderFilterDto filter)
    {
        try
        {
            var response = await _api.ListOrdersAsync(filter);

            var vm = new ShopSync.Web.Models.OrderIndexViewModel
            {
                Orders = response.Data ?? new(),
                CurrentPage = response.Page > 0 ? response.Page : 1,
                PageSize = response.PageSize > 0 ? response.PageSize : 20,
                TotalCount = response.Count,
                StatusFilter = filter.Status ?? string.Empty
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Hata: " + (ex.InnerException?.Message ?? ex.Message);
            return View(new ShopSync.Web.Models.OrderIndexViewModel());
        }
    }

    public async Task<IActionResult> Detail(string id)
    {
        try
        {
            var order = await _api.GetOrderAsync(id);
            var vm = new ShopSync.Web.Models.OrderDetailViewModel { Order = order };
            return View(vm);
        }
        catch
        {
            TempData["Error"] = "Sipariş bulunamadı.";
            return RedirectToAction(nameof(Index));
        }
    }


    // 3. YENİ SİPARİŞ OLUŞTURMA SAYFASI (Sadece Formu Gösterir)
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequestDto request)
    {
        try
        {
            var result = await _api.CreateOrderAsync(request);
            TempData["Success"] = "Sipariş başarıyla oluşturuldu!";
            return RedirectToAction(nameof(Detail), new { id = result.OrderId });
        }
        catch (Exception ex)
        {
            string detay = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            TempData["Error"] = "Çevirme Hatası: " + detay;

            // Yorumu kaldırdık, formu geri döndürüyoruz:
            return View(request);
        }
    }

    // 5. SİPARİŞ ONAYLAMA
    [HttpPost]
    public async Task<IActionResult> Confirm(string id, string note)
    {
        try
        {
            var request = new ConfirmOrderRequestDto { Note = note };
            await _api.ConfirmOrderAsync(id, request);

            TempData["Success"] = "Sipariş başarıyla onaylandı.";
        }
        catch
        {
            TempData["Error"] = "Sipariş onaylanırken hata oluştu.";
        }

        return RedirectToAction(nameof(Detail), new { id = id });
    }
    // 6. SİPARİŞ İPTAL ETME
    [HttpPost]
    public async Task<IActionResult> Cancel(string id, string reason)
    {
        try
        {
            var request = new CancelOrderRequestDto { Reason = reason };
            await _api.CancelOrderAsync(id, request);

            TempData["Success"] = "Sipariş iptal edildi.";
        }
        catch
        {
            TempData["Error"] = "Sipariş iptal edilirken hata oluştu.";
        }

        return RedirectToAction(nameof(Detail), new { id = id });
    }


    // 7. TOPLU İPTAL SAYFASI (Sadece Formu Gösterir)
    [HttpGet]
    public IActionResult BatchCancel()
    {
        return View();
    }

    // 8. TOPLU İPTAL İŞLEMİ (Formdan gelen virgüllü/satırlı ID'leri API'ye yollar)
    [HttpPost]
    public async Task<IActionResult> BatchCancel(string orderIds, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderIds))
            {
                TempData["Error"] = "En az bir Sipariş ID girmelisiniz.";
                return View();
            }

            // Metin kutusuna alt alta veya virgülle girilen ID'leri ayıklayıp listeye çeviriyoruz
            var idsList = orderIds.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) //stringsplitoptions.removeemptyentries ile boş satırları atıyoruz
                                  .Select(id => id.Trim())
                                  .ToList();

            var request = new BatchCancelRequestDto
            {
                OrderIds = idsList,
                Reason = reason
            };

            // API'ye yollayıp dönen sonuç listesini alıyoruz
            var response = await _api.BatchCancelAsync(request);

            // Sonuçları ekranda göstermek için View'a modeli yolluyoruz
            return View(response);
        }
        catch (Exception ex)
        {
            string detay = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            TempData["Error"] = "İptal Hatası: " + detay;
            return View();
        }
    }

    // ADMIN OVERRIDE
    [HttpPost]
    public async Task<IActionResult> AdminOverride(string id, string reason)
    {
        try
        {
            var request = new AdminOverrideRequestDto { Reason = reason };
            await _api.AdminOverrideAsync(id, request);
            TempData["Success"] = "Admin override uygulandı.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Admin override başarısız: " + (ex.InnerException?.Message ?? ex.Message);
        }
        return RedirectToAction(nameof(Detail), new { id = id });
    }
}
