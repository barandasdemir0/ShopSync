using Mapster;
using ShopSync.OrderService.Models;

namespace ShopSync.OrderService.Dtos;

public sealed class OrderMappingConfig : IRegister
{
    public  void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Order, OrderResponseDto>()
             .Map(dest => dest.StatusDescription, src => src.CurrentStatus.Description)
             .Map(dest => dest.Items, src => src.LineItems)
             .Map(dest => dest.History, src => src.History);

    }
}
