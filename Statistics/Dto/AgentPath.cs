using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;

namespace AuxiliumLab.AiSandbox.Domain.Statistics.Entities;

public record struct AgentPath(int Turn, Coordinates[] Path);

