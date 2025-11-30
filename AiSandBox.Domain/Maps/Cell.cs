using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Maps;

public class Cell
{
    public Coordinates Coordinates { get; init; }
    public bool IsHeroSight { get; internal set; }
    public bool IsEnemySight { get; internal set; }
    public SandboxBaseObject Object { get; internal set; }
}

