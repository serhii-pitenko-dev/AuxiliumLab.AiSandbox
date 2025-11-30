using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Agents.Entities;

public class Enemy: Agent
{
    public Enemy(
        Coordinates coordinates,
        InitialAgentCharacters characters,
        Guid id) : base(ECellType.Enemy, characters, coordinates, id) 
    {

    }

    public Enemy(): base(ECellType.Enemy, new InitialAgentCharacters(), new Coordinates(0,0), new Guid())
    { }

    public Enemy Clone()
    {
        var clone = new Enemy();

        CopyBaseTo(clone);

        return clone;
    }
}

