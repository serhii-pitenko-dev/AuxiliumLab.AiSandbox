using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;

namespace AiSandBox.ApplicationServices.Commands.Playground.InitializePlaygroundFromFile;

public class InitializePlaygroundFromFileCommandHandler(
    IFileDataManager<StandardPlayground> playgroundFileDataManager,
    IMemoryDataManager<StandardPlayground> playgroundMemoryDataManager,
    IMemoryDataManager<InitialPreconditions> initialPreconditionsMemoryDataManager) : IInitializePlaygroundFromFileCommandHandler
{
    public void Handle(InitializePlaygroundFromFileCommandParameters commandParameters)
    {
        // Load map from file
        StandardPlayground playground = playgroundFileDataManager.LoadObject(commandParameters.MapId);
        playground.LookAroundEveryone();
        // Save map to memory
        playgroundMemoryDataManager.AddOrUpdate(commandParameters.MapId, playground);

        // Calculate and save initial preconditions
        initialPreconditionsMemoryDataManager.AddOrUpdate(
            commandParameters.MapId,
            new InitialPreconditions(
                commandParameters.MapId,
                playground.MapWidth,
                playground.MapHeight,
                playground.MapArea,
                CalculateBlocksPercent(playground),
                CalculateEnemiesPercent(playground),
                playground.Blocks.Count,
                playground.Enemies.Count));
    }

    private static int CalculateBlocksPercent(StandardPlayground playground)
    {
        return playground.MapArea > 0 ? (int)Math.Round((double)playground.Blocks.Count / playground.MapArea * 100) : 0;
    }

    private static int CalculateEnemiesPercent(StandardPlayground playground)
    {
        return playground.MapArea > 0 ? (int)Math.Round((double)playground.Enemies.Count / playground.MapArea * 100) : 0;
    }
}