using AiSandBox.ApplicationServices.Orchestrators;
using AiSandBox.ApplicationServices.Queries.Maps;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapInitialPeconditions;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout.MapCellData;
using AiSandBox.ApplicationServices.Runner;
using AiSandBox.ConsolePresentation.Settings;
using AiSandBox.SharedBaseTypes.ValueObjects;
using ConsolePresentation;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace AiSandBox.ConsolePresentation;

public class ConsoleRunner : IConsoleRunner
{
    private readonly IExecutor _executor;
    private readonly IMapQueriesHandleService _mapQueries;
    private readonly ConsoleSize _consoleSize;
    private readonly ColorScheme _consoleColorScheme;
    private readonly int _movementTimeout;
    private Dictionary<ECellType, string> _cellData = [];
    private Guid _playgroundId;
    private PreconditionsResponse? _preconditionsResponse;
    public event Action<Guid>? ReadyForRendering;


    public ConsoleRunner(
        IExecutor executor,
        IMapQueriesHandleService mapQueries,
        IOptions<ConsoleSettings> consoleSettings,
        ITurnFinalizator turnFinalizator )
    {
        _executor = executor;
        _mapQueries = mapQueries;
        _consoleSize = consoleSettings.Value.ConsoleSize;
        _consoleColorScheme = consoleSettings.Value.ColorScheme;
        _movementTimeout = consoleSettings.Value.MovementTimeout;
    }

    public void Run()
    {
        // Initialize console
        InitializeConsole();

        // Subscribe to executor events
        _executor.GameStarted += OnGameStarted;
        _executor.TurnExecuted += OnTurnEnded;
        _executor.ExecutionFinished += OnExecutionFinished;
        // Start the game (business logic in executor)
        _executor.Run();

        // Cleanup
        _executor.GameStarted -= OnGameStarted;
        _executor.TurnExecuted -= OnTurnEnded;
        _executor.ExecutionFinished -= OnExecutionFinished;

        Console.ReadLine();
    }

    private void InitializeConsole()
    {
        Console.CursorVisible = false; // Hide the cursor
        InitializeElementsRendering();
        ResizeConsole(_consoleSize.Width, _consoleSize.Height);
        RenderBackground(_consoleColorScheme.GlobalBackGroundColor);
    }

    private void OnGameStarted(Guid playgroundId)
    {
        _playgroundId = playgroundId;
        RenderInitialGameInfo();
        RenderMap();
    }

    private void OnTurnEnded(Guid playgroundId)
    {
        RenderMapWithSequentialMovements();
    }

    private void RenderInitialGameInfo()
    {
        _preconditionsResponse = _mapQueries.MapInitialPreconditionsQuery.Get(_playgroundId);

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        WriteSysInfoLine($"Map initialized with ID: {_playgroundId}");
        WriteSysInfoLine($"Width: {_preconditionsResponse.Width}; Height: {_preconditionsResponse.Height}; Area: {_preconditionsResponse.Area}; Percent of blocks: {_preconditionsResponse.PercentOfBlocks}; Percent of enemies {_preconditionsResponse.PercentOfEnemies}");
        WriteSysInfoLine($"Initial elements count: blocks - {_preconditionsResponse.BlocksCount}; enemy - {_preconditionsResponse.EnemiesCount}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private void RenderMapWithSequentialMovements()
    {
        MapLayoutResponse mapRenderData = _mapQueries.MapLayoutQuery.GetFromMemory(_playgroundId);
        
        // Get all agents (hero + enemies) with their movement data
        var agentMovements = GetAgentMovements(mapRenderData);
        
        if (!agentMovements.Any())
        {
            // No movements to show, just render final state
            RenderMapSnapshot(mapRenderData);
            return;
        }

        // Get max path length to know how many steps to animate
        int maxSteps = agentMovements.Max(a => a.Path.Count);

        // Animate each step
        for (int step = 0; step < maxSteps; step++)
        {
            RenderMapSnapshot(mapRenderData, step);
            Thread.Sleep(_movementTimeout);
        }

        // Show final state
        RenderMapSnapshot(mapRenderData);
    }

    private List<AgentMovementData> GetAgentMovements(MapLayoutResponse mapRenderData)
    {
        var movements = new List<AgentMovementData>();
        
        int width = mapRenderData.Cells.GetLength(0);
        int height = mapRenderData.Cells.GetLength(1);

        // Scan the map for agents and extract their path data
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                MapCell cell = mapRenderData.Cells[x, y];
                
                if (cell.CellType == ECellType.Hero && cell.HeroLayer.IsPath)
                {
                    // Hero found - extract path from HeroLayer
                    var path = ExtractPathFromLayer(mapRenderData, true);
                    if (path.Any())
                    {
                        movements.Add(new AgentMovementData
                        {
                            OrderInQueue = 0, // Hero always goes first
                            AgentType = ECellType.Hero,
                            Path = path
                        });
                    }
                }
                else if (cell.CellType == ECellType.Enemy && cell.EnemyLayer.IsPath)
                {
                    // Enemy found - extract path from EnemyLayer
                    var path = ExtractPathFromLayer(mapRenderData, false, x, y);
                    if (path.Any())
                    {
                        // Enemies start from order 1
                        int order = GetEnemyOrder(x, y, mapRenderData);
                        movements.Add(new AgentMovementData
                        {
                            OrderInQueue = order,
                            AgentType = ECellType.Enemy,
                            Path = path
                        });
                    }
                }
            }
        }

        return movements.OrderBy(m => m.OrderInQueue).ToList();
    }

    private List<Coordinates> ExtractPathFromLayer(MapLayoutResponse mapRenderData, bool isHero, int? startX = null, int? startY = null)
    {
        var path = new List<Coordinates>();
        int width = mapRenderData.Cells.GetLength(0);
        int height = mapRenderData.Cells.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                MapCell cell = mapRenderData.Cells[x, y];
                bool hasPath = isHero ? cell.HeroLayer.IsPath : cell.EnemyLayer.IsPath;
                
                if (hasPath)
                {
                    // Convert screen coordinates back to Cartesian
                    int cartesianY = height - 1 - y;
                    path.Add(new Coordinates(x, cartesianY));
                }
            }
        }

        return path;
    }

    private int GetEnemyOrder(int x, int y, MapLayoutResponse mapRenderData)
    {
        // This is a placeholder - you'll need to enhance MapLayoutResponse 
        // to include OrderInTurnQueue data from agents
        // For now, return a default order
        return 1;
    }

    private void RenderMapSnapshot(MapLayoutResponse mapRenderData, int? currentStep = null)
    {
        int width = mapRenderData.Cells.GetLength(0);
        int height = mapRenderData.Cells.GetLength(1);

        // Clear previous map rendering area
        Console.SetCursorPosition(0, 6);

        WriteSysInfoLine($" Turn: {mapRenderData.turnNumber}");

        // Render top border: full line of █ with empty cell at the start
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}] {new string('█', width + 2)}[/]");


        // Render map rows
        for (int y = 0; y < height; y++)
        {
            string row = string.Empty;
            
            for (int x = 0; x < width; x++)
            {
                MapCell cell = mapRenderData.Cells[x, y];
                row += GetCellSymbol(cell);
            }

            int yCoordinate = (height - 1) - y;
            string leftBorder = (yCoordinate < 10) 
                ? yCoordinate.ToString() 
                : "█";

            // Added empty cell before left border
            AnsiConsole.MarkupLine(
                $"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}] {leftBorder}[/]" +
                $"{row}" +
                $"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]");
        }

        // Render bottom border with extra space
        string bottomBorder = " █";
        for (int x = 0; x < width; x++)
        {
            bottomBorder += (x < 10) ? x.ToString() : "█";
        }
        bottomBorder += "█";
        
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]{bottomBorder}[/]");

        RenderBottomData(mapRenderData);
    }

    private void RenderMap()
    {
        RenderMapSnapshot(_mapQueries.MapLayoutQuery.GetFromMemory(_playgroundId));
    }

    private string GetCellSymbol(MapCell cell)
    {
        if (cell.CellType == ECellType.Empty && cell.HeroLayer.IsPath)
        {
            return $"[#000000 on {_consoleColorScheme.HeroPathColor}]·[/]";
        }
        else if (cell.CellType == ECellType.Empty && cell.EnemyLayer.IsPath)
        {
            return $"[#000000 on {_consoleColorScheme.EnemyPathColor}]·[/]";
        }
        else if (cell.CellType == ECellType.Empty && cell.HeroLayer.IsAgentSight)
        {
            return $"[#000000 on {_consoleColorScheme.HeroVisionColor}]·[/]";
        }
        else if (cell.CellType == ECellType.Empty && cell.EnemyLayer.IsAgentSight)
        {
            return $"[#000000 on {_consoleColorScheme.EnemyVisionColor}]·[/]";
        }
        else
        {
            return _cellData[cell.CellType];
        }
    }

    private void RenderBottomData(MapLayoutResponse mapRenderData)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private void WriteSysInfoLine(string message)
    {
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.GlobalBackGroundColor}]{message}[/]");
    }

    private void InitializeElementsRendering()
    {
        _cellData = new Dictionary<ECellType, string>
        {
            { ECellType.Empty, $"[#000000 on {_consoleColorScheme.MapBackGroundColor}]·[/]" },
            { ECellType.Block, $"[{_consoleColorScheme.BlockColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]" },
            { ECellType.Hero, $"[{_consoleColorScheme.HeroColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]" },
            { ECellType.Enemy, $"[{_consoleColorScheme.EnemyColor} on {_consoleColorScheme.MapBackGroundColor}]X[/]" },
            { ECellType.Exit, $"[Black on Green]E[/]" }
        };
    }

    private static void ResizeConsole(int width, int height)
    {
        // Make sure buffer is at least as big as window
        if (Console.LargestWindowWidth < width)
            width = Console.LargestWindowWidth;

        if (Console.LargestWindowHeight < height)
            height = Console.LargestWindowHeight;

        // First set buffer (>= window)
        Console.SetBufferSize(width, height);

        // Then set window
        Console.SetWindowSize(width, height);
    }

    private static void RenderBackground(string backgroundColor)
    {
        Console.Clear();
        var width = Console.WindowWidth;
        var height = Console.WindowHeight;

        for (int i = 0; i < height; i++)
            AnsiConsole.MarkupLine($"[#000000 on {backgroundColor}]{new string(' ', width)}[/]");

        Console.SetCursorPosition(0, 0);
    }

    private void OnExecutionFinished(Guid playgroundId, ESandboxStatus turnStatus)
    {
        switch (turnStatus)
        {
            case ESandboxStatus.HeroWon:
                OnGameWon();
                break;
            case ESandboxStatus.HeroLost:
                OnGameLost();
                break;
            case ESandboxStatus.TurnLimitReached:
                OnTurnLimitReached();
                break;
            default:
                WriteSysInfoLine($"Unknown game status: {turnStatus}");
                break;
        }
    }

    private void OnGameWon()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[Green on {_consoleColorScheme.GlobalBackGroundColor}]!!! HERO WIN !!![/]");
        RenderMap();
    }

    private void OnGameLost()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[Red on {_consoleColorScheme.GlobalBackGroundColor}]!!! HERO LOST !!![/]");
        RenderMap();
    }

    private void OnTurnLimitReached()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[Red on {_consoleColorScheme.GlobalBackGroundColor}]!!! TURN LIMIT REACHED !!![/]");
        RenderMap();
    }

    // Helper class to store agent movement data
    private class AgentMovementData
    {
        public int OrderInQueue { get; set; }
        public ECellType AgentType { get; set; }
        public List<Coordinates> Path { get; set; } = new();
    }
}