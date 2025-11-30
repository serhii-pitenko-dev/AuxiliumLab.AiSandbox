using AiSandBox.SharedBaseTypes.ValueObjects;
using System.Text.Json.Serialization;

namespace AiSandBox.Domain;

public abstract class SandboxBaseObject
{
    public ECellType Type { get; protected set; }
    public Guid Id { get; init; }
    
    [JsonInclude]
    public Coordinates Coordinates { get; protected set; }
    
    [JsonInclude]
    public bool Transparent { get; protected set; }

    // Parameterless constructor for deserialization
    protected SandboxBaseObject()
    {
    }

    // Primary constructor
    protected SandboxBaseObject(ECellType type, Coordinates coordinates, Guid id)
    {
        Type = type;
        Coordinates = coordinates;
        Id = id;
    }

    public virtual void UpdateCoordinates(Coordinates newCoordinates)
    {
        Coordinates = newCoordinates;
    }
}
