using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.InanimateObjects;

public class EmptyCell : SandboxBaseObject
{
    public EmptyCell(Coordinates coordinates, Guid id) : base(ECellType.Empty, coordinates, id) 
    { 
        Transparent = true;
    }
}