using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.InanimateObjects;

public class Exit : SandboxBaseObject
{
    public Exit(Coordinates coordinates, Guid id) : base(ECellType.Exit, coordinates, id) 
    { 
        Transparent = true;
    }
}

