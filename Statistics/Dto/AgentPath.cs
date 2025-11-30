using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Statistics.Entities;

public record struct AgentPath(int Turn, Coordinates[] Path);

