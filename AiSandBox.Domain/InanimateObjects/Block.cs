using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.InanimateObjects;

public class Block: SandboxBaseObject
{
    public Block(Coordinates coordinates, Guid id) : base(ECellType.Block, coordinates, id) 
    { 
        Transparent = false;
    }
}