using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Agents.Entities;

public class Hero : Agent
{
    public Hero(Coordinates coordinates,
        InitialAgentCharacters characters,
        Guid id) : base(ECellType.Hero, characters, coordinates, id)
    {

    }

    public Hero() : base(ECellType.Hero, new InitialAgentCharacters(),  new Coordinates(0, 0), new Guid())
    { }

    public Hero Clone()
    {
        var clone = new Hero();

        CopyBaseTo(clone);

        return clone;
    }
}

