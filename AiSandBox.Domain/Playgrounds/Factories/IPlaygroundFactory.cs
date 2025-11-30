using AiSandBox.Domain.Agents.Entities;

namespace AiSandBox.Domain.Playgrounds.Factories;

public interface IPlaygroundFactory
{
    public StandardPlayground CreateStandard(
        InitialAgentCharacters heroCharacters,
        InitialAgentCharacters enemyCharacters,
        int width, 
        int height, 
        int percentOfBlocks = 10, 
        int percentOfEnemies = 0);

}

