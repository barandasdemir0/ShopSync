using ShopSync.InventoryService.Infrastructure.Exceptions;

namespace ShopSync.InventoryService.Extension;

public static class GrpcExtensions
{
    public static WebApplicationBuilder AddGrpcServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<GrpcExceptionInterceptor>();
        });

        return builder;
    }
}
