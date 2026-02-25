using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Queries.Map.Entities;

public record struct MapCell(
    Coordinates Coordinates,
    Guid ObjectId,
    ObjectType ObjectType,
    AgentEffect[] Effects);