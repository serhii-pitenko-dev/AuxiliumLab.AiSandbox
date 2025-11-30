using AiSandBox.Domain.Agents.Entities;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Agents.Factories;

public interface IHeroFactory
{
    Hero CreateHero(Coordinates coordinates, InitialAgentCharacters characters);
}

