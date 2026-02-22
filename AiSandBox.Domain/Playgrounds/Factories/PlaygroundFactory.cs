using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Validation;
using AiSandBox.Domain.Playgrounds.Builders;
using AiSandBox.Domain.Maps;

namespace AiSandBox.Domain.Playgrounds.Factories;

public class PlaygroundFactory(IPlaygroundBuilder playgroundBuilder) : IPlaygroundFactory
{
    public static int PercentCalculation (int totalCells, int percent) => (totalCells * percent) / 100;

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
            .PlaceBlocks(PercentCalculation(width * height, percentOfBlocks))
            .PlaceHero(heroCharacters)
            .PlaceExit()
            .PlaceEnemies(PercentCalculation(width * height, percentOfEnemies), enemyCharacters)
            .FillCellGrid()
            .Build();
    }
}


