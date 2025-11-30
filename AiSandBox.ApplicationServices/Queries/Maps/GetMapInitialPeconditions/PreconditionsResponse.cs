namespace AiSandBox.ApplicationServices.Queries.Maps.GetMapInitialPeconditions;

public record PreconditionsResponse(
    Guid MapId,
    int Width,
    int Height,
    int Area,
    int PercentOfBlocks,
    int PercentOfEnemies,
    int BlocksCount,
    int EnemiesCount);

