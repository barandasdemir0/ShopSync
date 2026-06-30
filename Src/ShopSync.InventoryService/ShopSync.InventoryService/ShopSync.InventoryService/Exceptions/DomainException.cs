namespace ShopSync.InventoryService.Exceptions;

public sealed class DomainException : Exception
{
    public string Code { get; }
    public DomainException(string message, string code = "DOMAIN_ERROR") : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Hata kodu boş bırakılamaz.", nameof(code));
        }
            
        Code = code;
    }
}
