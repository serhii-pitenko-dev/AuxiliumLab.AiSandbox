using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout.MapCellData;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.ApplicationServices.Converters.Maps;

public static class MapSquareCellsConverter
{
    public static MapLayoutResponse ToMapLayout(this StandardPlayground playground)
    {
        MapCell[,] cells = new MapCell[playground.MapWidth, playground.MapHeight];

        for (int x = 0; x < playground.MapWidth; x++)
        {
            for (int screenY = 0; screenY < playground.MapHeight; screenY++)
            {
                // Convert screen coordinates to Cartesian coordinates
                // Screen: Y=0 is top, Y=Height-1 is bottom
                // Cartesian: Y=0 is bottom, Y=Height-1 is top
                int cartesianY = playground.MapHeight - 1 - screenY;

                var currentCell = playground.GetCell(x, cartesianY);

                // Check if hero is at current Cartesian position
                // Use Hero's Coordinates, not cell type, as the source of truth
                bool isHeroAtCell = playground.Hero?.Coordinates.X == x &&
                                   playground.Hero?.Coordinates.Y == cartesianY;

                // Determine actual cell type - hero position overrides cell grid type
                ECellType actualCellType = currentCell.Object.Type;

                cells[x, screenY] = new MapCell
                {
                    CellType = actualCellType,
                    HeroLayer = new AgentLayer
                    {
                        IsAgentSight = currentCell.IsHeroSight,
                        //!!! should be refactored to not check every cell
                        IsPath = playground.Hero?.PathToTarget.Any(coord =>
                            coord.X == x &&
                            coord.Y == cartesianY) ?? false
                    },
                    EnemyLayer = new AgentLayer
                    {
                        IsAgentSight = currentCell.IsEnemySight,
                        //!!! should be refactored to not check every cell
                        IsPath = playground.Enemies.Any(enemy =>
                            enemy.PathToTarget.Any(coord =>
                                coord.X == x &&
                                coord.Y == cartesianY))
                    }
                };
            }
        }

        var mapLayoutResponse = new MapLayoutResponse(playground.Turn, cells);

        return mapLayoutResponse;
    }
}

