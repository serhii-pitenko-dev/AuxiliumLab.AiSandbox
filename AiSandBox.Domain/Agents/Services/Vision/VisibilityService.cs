using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Agents.Services.Vision;

public class VisibilityService : IVisibilityService
{
    public void UpdateVisibleCells(Agent agent, StandardPlayground playground)
    {
        // Clear previous sight flags for this agent
        foreach (var previouslyVisibleCell in agent.VisibleCells)
        {
            if (agent.Type == ECellType.Hero)
            {
                previouslyVisibleCell.IsHeroSight = false;
            }
            else if (agent.Type == ECellType.Enemy)
            {
                previouslyVisibleCell.IsEnemySight = false;
            }
        }

        Cell[,] range = playground.CutMapPart(agent.Coordinates, agent.SightRange);

        agent.VisibleCells.Clear();
     
        // Find the hero's actual position in the extracted grid
        int heroX = -1, heroY = -1;
        for (int x = 0; x < range.GetLength(0); x++)
        {
            for (int y = 0; y < range.GetLength(1); y++)
            {
                if (range[x, y].Coordinates.X == agent.Coordinates.X &&
                    range[x, y].Coordinates.Y == agent.Coordinates.Y)
                {
                    heroX = x;
                    heroY = y;
                    break;
                }
            }
            if (heroX != -1) break;
        }

        // If hero not found in range (shouldn't happen), fall back to center
        if (heroX == -1 || heroY == -1)
        {
            heroX = range.GetLength(0) / 2;
            heroY = range.GetLength(1) / 2;
        }

        // Iterate through all cells in the sight range
        for (int x = 0; x < range.GetLength(0); x++)
        {
            for (int y = 0; y < range.GetLength(1); y++)
            {
                Cell targetCell = range[x, y];

                // Skip the agent's own position by checking actual coordinates
                if (targetCell.Coordinates.X == agent.Coordinates.X &&
                    targetCell.Coordinates.Y == agent.Coordinates.Y)
                    continue;

                // Check if there's a clear line of sight to this cell
                if (HasLineOfSight(range, heroX, heroY, x, y))
                {
                    // Get the actual cell from the map to persist the sight changes
                    Cell originalCell = playground.GetCell(targetCell.Coordinates);

                    if (agent.Type == ECellType.Hero)
                    {
                        originalCell.IsHeroSight = true;
                    }
                    else if (agent.Type == ECellType.Enemy)
                    {
                        originalCell.IsEnemySight = true;
                    }

                    agent.VisibleCells.Add(originalCell);
                }
            }
        }
    }

    private bool HasLineOfSight(Cell[,] grid, int startX, int startY, int endX, int endY)
    {
        int dx = Math.Abs(endX - startX);
        int dy = Math.Abs(endY - startY);
        int sx = startX < endX ? 1 : -1;
        int sy = startY < endY ? 1 : -1;
        int err = dx - dy;

        int currentX = startX;
        int currentY = startY;

        while (true)
        {
            // If we've reached the target cell, it's visible
            if (currentX == endX && currentY == endY)
                return true;

            // Check bounds before accessing the array
            if (currentX < 0 || currentX >= grid.GetLength(0) ||
                currentY < 0 || currentY >= grid.GetLength(1))
            {
                return false;
            }

            // Check if current cell blocks vision (but not the starting position)
            if (!(currentX == startX && currentY == startY))
            {
                Cell currentCell = grid[currentX, currentY];
                if (currentCell != null && !currentCell.Object.Transparent)
                {
                    // This cell blocks vision, so target is not visible
                    return false;
                }
            }

            // Bresenham's line algorithm
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                currentX += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                currentY += sy;
            }
        }
    }
}

