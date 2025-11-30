using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout.MapCellData;

namespace AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;

public record MapLayoutResponse(int turnNumber, MapCell[,] Cells);

