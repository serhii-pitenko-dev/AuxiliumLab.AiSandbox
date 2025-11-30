using AiSandBox.Infrastructure.Configuration.Preconditions;

namespace Infrastructure.Configuration;

public class ConfigurationData
{
    public SandBoxConfiguration MapSettings { get; init; } = new();
}