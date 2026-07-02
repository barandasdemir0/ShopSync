namespace ShopSync.OrderService.Exceptions;

public sealed class DomainException :  Exception
{
    // Hata kodu (Örn: "ORDER_INVALID_STATE_TRANSITION")
    public string Code { get; }
    public DomainException(string message, string code = "DOMAIN_ERROR")
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Hata kodu boş bırakılamaz.", nameof(code));
        }
            
        Code = code;
    }
}
