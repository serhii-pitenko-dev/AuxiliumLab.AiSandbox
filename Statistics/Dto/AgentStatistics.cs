using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Statistics.Entities;

public record struct AgentStatistics(
    Guid id, 
    int Turn, 
    ECellType CellType,
    AgentPath[] Path);
