using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Validation;
using AiSandBox.Domain.Playgrounds.Builders;
using AiSandBox.Domain.Maps;

namespace AiSandBox.Domain.Playgrounds.Factories;

public class PlaygroundFactory(IPlaygroundBuilder playgroundBuilder) : IPlaygroundFactory
{
    public StandardPlayground CreateStandard(
        InitialAgentCharacters heroCharacters,
        InitialAgentCharacters enemyCharacters,
        int width, 
        int height,
        int percentOfBlocks = 10,
        int percentOfEnemies = 0)
    {
        MapValidator.ValidateSize(width, height);
        MapValidator.ValidateElementsProportion(percentOfBlocks, percentOfEnemies);

        return playgroundBuilder.SetMap(new MapSquareCells(width, height))
            .PlaceBlocks(percentOfBlocks)
            .PlaceHero(heroCharacters)
            .PlaceExit()
            .PlaceEnemies(percentOfEnemies, enemyCharacters)
            .FillCellGrid()
            .Build();
    }
}


