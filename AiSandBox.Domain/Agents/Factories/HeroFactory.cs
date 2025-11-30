using AiSandBox.Domain.Agents.Entities;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Agents.Factories;

public class HeroFactory: IHeroFactory
{
    public Hero CreateHero(Coordinates coordinates, InitialAgentCharacters characters)
    {
        return new Hero(coordinates, characters, Guid.NewGuid());
    }
}

