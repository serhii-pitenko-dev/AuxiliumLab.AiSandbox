using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Infrastructure.Configuration.Preconditions;

public struct MapConfiguration
{
    public Size Size { get; set; }
    public ElementsPercentages ElementsPercentages { get; set; }
    public EMapType Type { get; set; }
    public FileSource FileSource { get; set; }
}