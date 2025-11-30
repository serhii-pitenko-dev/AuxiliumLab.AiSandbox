using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Playgrounds.Factories;
using AiSandBox.Domain.State;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;

public class CreatePlaygroundCommandHandler(
    IPlaygroundFactory playgroundFactory,
    IMemoryDataManager<StandardPlayground> playgroundMemoryDataManager,
    IMemoryDataManager<InitialPreconditions> initialPreconditionsMemoryDataManager) : ICreatePlaygroundCommandHandler
{
    public Guid Handle(CreatePlaygroundCommandParameters commandParameters)
    {
        StandardPlayground playground = commandParameters.MapConfiguration.Type switch
        {
            EMapType.Standard => playgroundFactory.CreateStandard(
                            new InitialAgentCharacters(
                                speed: commandParameters.HeroConfiguration.Speed,
                                sightRange: commandParameters.HeroConfiguration.SightRange,
                                stamina: commandParameters.HeroConfiguration.Stamina),
                            new InitialAgentCharacters(
                                speed: commandParameters.EnemyConfiguration.Speed,
                                sightRange: commandParameters.EnemyConfiguration.SightRange,
                                stamina: commandParameters.EnemyConfiguration.Stamina),
                            commandParameters.MapConfiguration.Size.Height,
                            commandParameters.MapConfiguration.Size.Width,
                            commandParameters.MapConfiguration.ElementsPercentages.BlocksPercent,
                            commandParameters.MapConfiguration.ElementsPercentages.PercentOfEnemies),
            _ => throw new NotImplementedException("Map type not implemented"),
        };

        Guid playgroundId = playground.Id;
        playgroundMemoryDataManager.AddOrUpdate(playgroundId, playground);

        initialPreconditionsMemoryDataManager.AddOrUpdate(
            playgroundId,
            new InitialPreconditions(
                playgroundId,
                playground.MapHeight,
                playground.MapHeight,
                playground.MapHeight,
                commandParameters.MapConfiguration.ElementsPercentages.BlocksPercent,
                commandParameters.MapConfiguration.ElementsPercentages.PercentOfEnemies,
                playground.Blocks.Count,
                playground.Enemies.Count));

        return playgroundId;   
    }
}