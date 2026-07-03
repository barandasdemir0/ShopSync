using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace ShopSync.OrderService.Exceptions;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }


    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = (int)HttpStatusCode.InternalServerError;
        var message = "Sunucu hatası oluştu. Lütfen daha sonra tekrar deneyin.";
        var errorCode = "INTERNAL_ERROR";


        if (exception is DomainException domainException)
        {
            statusCode = StatusCodes.Status422UnprocessableEntity;
            message = domainException.Message;
            errorCode = domainException.Code;

            _logger.LogWarning(
                exception,
                "İş kuralı hatası: {Code} - {Message}",
                errorCode,
                message);
        }

        else if (exception is ArgumentException)
        {
            statusCode = StatusCodes.Status400BadRequest;
            message = exception.Message;
            errorCode = "INVALID_ARGUMENT";

            _logger.LogWarning(
                exception,
                "Geçersiz parametre: {Message}",
                message);
        }
        else
        {
            _logger.LogError(
                exception,
                "Beklenmeyen hata oluştu.");
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            code = errorCode,
            message
        }, cancellationToken);

        return true;

    }
}
