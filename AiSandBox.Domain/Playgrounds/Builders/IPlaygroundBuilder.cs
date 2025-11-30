using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Maps;

namespace AiSandBox.Domain.Playgrounds.Builders;

public interface IPlaygroundBuilder
{
    IPlaygroundBuilder SetMap(MapSquareCells tileMap);
    IPlaygroundBuilder PlaceBlocks(int percentOfBlocks);
    IPlaygroundBuilder PlaceHero(InitialAgentCharacters heroCharacters);
    IPlaygroundBuilder PlaceExit();
    IPlaygroundBuilder PlaceEnemies(int percentOfEnemies, InitialAgentCharacters enemyCharacters);
    IPlaygroundBuilder FillCellGrid();
    StandardPlayground Build();
}

