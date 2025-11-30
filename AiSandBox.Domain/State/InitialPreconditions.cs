namespace AiSandBox.Domain.State;

public record InitialPreconditions(
    Guid MapId, 
    int Width, 
    int Height, 
    int Area, 
    int PercentOfBlocks, 
    int PercentOfEnemies,
    int BlocksCount,
    int EnemiesCount);

