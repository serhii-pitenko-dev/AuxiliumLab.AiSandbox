using AiSandBox.Infrastructure.Configuration.Preconditions;

namespace AiSandBox.Infrastructure.Configuration;

public class ConfigurationData
{
    public SandBoxConfiguration MapSettings { get; init; } = new();
}