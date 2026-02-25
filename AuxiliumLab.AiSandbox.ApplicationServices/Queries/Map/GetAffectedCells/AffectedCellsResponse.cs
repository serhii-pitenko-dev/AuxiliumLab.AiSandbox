using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Map.Entities;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetAffectedCells;

public record AffectedCellsResponse(int TurnNumber, List<MapCell> Cells);