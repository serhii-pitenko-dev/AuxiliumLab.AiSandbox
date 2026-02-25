
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Map.Entities;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetMapLayout;

public record MapLayoutResponse(int turnNumber, MapCell[,] Cells);