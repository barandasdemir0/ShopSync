using Grpc.Core;
using Grpc.Core.Interceptors;
using ShopSync.InventoryService.Exceptions;

namespace ShopSync.InventoryService.Infrastructure.Exceptions;

public sealed class GrpcExceptionInterceptor : Interceptor
{
    private readonly ILogger<GrpcExceptionInterceptor> _logger;
    public GrpcExceptionInterceptor(ILogger<GrpcExceptionInterceptor> logger)
    {
        _logger = logger;
    }

    //unary server handler, gRPC sunucusuna gelen tekli (unary) istekleri işleyen bir interceptor metodudur. Bu metod, gelen isteği işlerken oluşabilecek hataları yakalar ve uygun gRPC hata kodları ile istemciye iletir.
    //unarynin amacı, istemciden gelen tek bir isteği alıp işlemek ve ardından tek bir yanıt döndürmektir. Bu, gRPC'nin temel iletişim modelidir ve genellikle CRUD işlemleri gibi basit istekler için kullanılır.
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            // İsteği normal akışında çalıştır
            return await continuation(request, context);
        }
        catch (ArgumentException ex)
        {
            // Parametre geçersiz hatası (Örn: SKU boş geldi)
            _logger.LogWarning(ex, "Geçersiz parametre hatası alındı.");

            // Adım adım gRPC hatası oluşturma
            StatusCode statusCode = StatusCode.InvalidArgument;// InvalidArgument, istemcinin gönderdiği parametrelerin geçersiz olduğunu belirtir. Bu, istemci tarafında bir hata olduğunu ve isteğin doğru şekilde yapılandırılması gerektiğini ifade eder.
            string errorMessage = ex.Message;
            Status grpcStatus = new Status(statusCode, errorMessage);

            RpcException rpcException = new RpcException(grpcStatus);
            throw rpcException;
        }
        catch (DomainException ex)
        {
            // Diğer tüm hatalar için genel bir hata mesajı döndür
            _logger.LogWarning(ex,"İş kuralı ihlali : {Message} - Kod: {Code}", ex.Message, ex.Code);

            // Adım adım Metadata oluşturma metadatanın amacı istemciye hatanın türünü ve detaylarını iletmektir. Bu, istemcinin hatayı daha iyi anlamasına ve uygun şekilde yanıt vermesine yardımcı olur.
            string metadataKey = "error-code"; 
            string metadataValue = ex.Code;
            Metadata grpcMetadata = new Metadata// Metadata nesnesi oluşturuluyor
            {
                { 
                    metadataKey, metadataValue 
                } // Metadata'ya hata kodu ekleniyor
            }; 

            // Adım adım gRPC hatası oluşturma
            StatusCode statusCode = StatusCode.FailedPrecondition; // FailedPrecondition, istemcinin isteği yerine getirebilmesi için belirli bir ön koşulu sağlamadığını belirtir. Bu, iş kuralı ihlallerini temsil etmek için uygun bir durum kodudur.
            string errorMessage = ex.Message;
            Status grpcStatus = new Status(statusCode, errorMessage);

            RpcException rpcException = new RpcException(grpcStatus, grpcMetadata);
            throw rpcException;
        }

        catch (Exception ex)
        {
            // Beklenmeyen sistemsel hatalar (Veritabanı çöktü, null reference oluştu vb.)
            _logger.LogError(ex, "gRPC servisinde beklenmeyen bir sunucu hatası oluştu.");

            // Dışarıya iç detayları sızdırmamak için genel bir hata mesajı dönüyoruz 
            StatusCode statusCode = StatusCode.Internal; // internalın anlamı sunucu tarafında bir hata oluştuğudur. Bu, istemciye hatanın sunucu tarafında olduğunu belirtir.
            string errorMessage = "Sistemsel bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.";
            Status grpcStatus = new Status(statusCode, errorMessage);
            RpcException rpcException = new RpcException(grpcStatus);
            throw rpcException;
        }
    }

}
