using AiSandBox.Infrastructure.MemoryManager;

namespace AiSandBox.ApplicationServices.Queries.Maps.GetMapInitialPeconditions;

public class InitialPreconditionsHandle(IMemoryDataManager<Domain.State.InitialPreconditions> initialPreconditionsMemoryDataManager) : IInitialPreconditions
{
    public PreconditionsResponse Get(Guid mapGuid)
    {
        var initialPreconditions = initialPreconditionsMemoryDataManager.LoadObject(mapGuid);
        
        return new PreconditionsResponse(
            initialPreconditions.MapId,
            initialPreconditions.Width,
            initialPreconditions.Height,
            initialPreconditions.Area,
            initialPreconditions.PercentOfBlocks,
            initialPreconditions.PercentOfEnemies,
            initialPreconditions.BlocksCount,
            initialPreconditions.EnemiesCount);
    }
}

