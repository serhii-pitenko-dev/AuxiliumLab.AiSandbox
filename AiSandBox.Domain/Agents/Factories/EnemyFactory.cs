using AiSandBox.Domain.Agents.Entities;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Agents.Factories;

public class EnemyFactory : IEnemyFactory
{
    public Enemy CreateEnemy(Coordinates coordinates, InitialAgentCharacters characters)
    {
        return new Enemy(coordinates, characters, Guid.NewGuid());
    }
}