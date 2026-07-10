namespace ShopSync.Web.Dtos;


public sealed class PagedResponseDto<T>
{
    // İşlem başarılı mı?
    public bool Success { get; set; } = true;

    // Şu an kaçıncı sayfadayız? (Örn: 1)
    public int Page { get; set; }

    // Bir sayfada kaç kayıt var? (Örn: 20)
    public int PageSize { get; set; }

    // Sadece bu sayfada dönen kayıt sayısı (Örn: 20)
    public int Count { get; set; }

    // Asıl verilerimiz (Listenin kendisi)
    public List<T> Data { get; set; } = new();
}
