using ShopSync.InventoryService.Infrastructure.Locking;
using ShopSync.InventoryService.Infrastructure.Persistence;
using ShopSync.InventoryService.Protos;
using ShopSync.InventoryService.Repositories;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService: InventoryGrpc.InventoryGrpcBase
{
    private readonly IInventoryRepository _repository;
    private readonly IDistributedLockService _lockService;
    private readonly MongoDbContext _dbContext;
    private readonly ILogger<InventoryGrpcService> _logger;

    public InventoryGrpcService(IInventoryRepository repository, IDistributedLockService lockService, MongoDbContext dbContext, ILogger<InventoryGrpcService> logger)
    {
        _repository = repository;
        _lockService = lockService;
        _dbContext = dbContext;
        _logger = logger;
    }

  


}
