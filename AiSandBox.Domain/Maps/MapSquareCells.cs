using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.InanimateObjects;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Maps;

public class MapSquareCells
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Area { get; init; } = new();
    private readonly Cell[,] _cellGrid;
    internal Cell[,] CellGrid => _cellGrid;

    public MapSquareCells(int width, int height)
    {
        Width = width;
        Height = height;
        Area = Width * Height;
        _cellGrid = new Cell[Width, Height];

        // Initialize every cell in the grid
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _cellGrid[x, y] = new Cell
                {
                    Coordinates = new Coordinates(x, y),
                    Object = new EmptyCell(new Coordinates(x, y), Guid.NewGuid())
                };
            }
        }
    }

    internal void MoveObject(Coordinates from, List<Coordinates> path)
    {
        Coordinates to = path.Last();
        Cell initialCell = _cellGrid[from.X, from.Y];
        Cell targetCell = _cellGrid[to.X, to.Y];
        if (initialCell.Object.Type == ECellType.Empty)
        {
            throw new InvalidOperationException("No object to move at the source coordinates.");
        }
        if (targetCell.Object.Type != ECellType.Empty)
        {
            throw new InvalidOperationException("Target cell is already occupied.");
        }

        // Move the object
        targetCell.Object = initialCell.Object;
        targetCell.Object.UpdateCoordinates(targetCell.Coordinates);
        if (targetCell.Object is Agent agent)
        {
            agent.AddToPath(path);
        }

        initialCell.Object = new EmptyCell(initialCell.Coordinates, Guid.NewGuid());
    }

    internal Cell[,] CutOutPartOfTheMap(Coordinates point, int radius)
    {
        // Calculate the bounds of the cutout area
        int startX = Math.Max(0, point.X - radius);
        int endX = Math.Min(Width - 1, point.X + radius);
        int startY = Math.Max(0, point.Y - radius);
        int endY = Math.Min(Height - 1, point.Y + radius);

        // Calculate dimensions of the result array
        int width = endX - startX + 1;
        int height = endY - startY + 1;

        // Create the result array
        Cell[,] result = new Cell[width, height];

        // Copy the cells from the map to the result array
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = _cellGrid[startX + x, startY + y];
            }
        }

        return result;
    }

    internal void PlaceObject(SandboxBaseObject obj)
    {
        Cell targetCell = _cellGrid[obj.Coordinates.X, obj.Coordinates.Y];
        if (targetCell.Object.Type != ECellType.Empty)
        {
            throw new InvalidOperationException("Target cell is already occupied.");
        }
        targetCell.Object = obj;
    }
}