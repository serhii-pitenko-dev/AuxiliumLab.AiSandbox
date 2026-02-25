using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;

namespace AuxiliumLab.AiSandbox.Domain.Statistics.Entities;

public record struct AgentStatistics(
    Guid id, 
    int Turn, 
    ObjectType CellType,
    AgentPath[] Path);
