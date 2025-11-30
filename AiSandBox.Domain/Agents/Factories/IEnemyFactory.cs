using AiSandBox.Domain.Agents.Entities;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Agents.Factories;

public interface IEnemyFactory
{
    Enemy CreateEnemy(Coordinates coordinates, InitialAgentCharacters characters);
}

