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
        RenderMap();
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

    private void RenderMap()
    {
        MapLayoutResponse mapRenderData = _mapQueries.MapLayoutQuery.GetFromMemory(_playgroundId);
        
        int width = mapRenderData.Cells.GetLength(0);   // First dimension is Width (X)
        int height = mapRenderData.Cells.GetLength(1);  // Second dimension is Height (Y)

        // Clear previous map rendering area
        Console.SetCursorPosition(0, 6); // Position after initial info

        WriteSysInfoLine($"Turn: {mapRenderData.turnNumber}");
        
        // Render top border: full line of █
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]{new string('█', width + 2)}[/]");

        // Render map rows (iterate through Y coordinates from top to bottom)
        for (int y = 0; y < height; y++)
        {
            string row = string.Empty;
            
            // Build row by iterating through X coordinates from left to right
            for (int x = 0; x < width; x++)
            {
                MapCell cell = mapRenderData.Cells[x, y];
                row += GetCellSymbol(cell);
            }

            // Calculate the Y-coordinate for this row (cartesian: bottom = 0, top = height-1)
            int yCoordinate = (height - 1) - y;
            string leftBorder = (yCoordinate < 10) 
                ? yCoordinate.ToString() 
                : "█";

            // Left border (number or █) + row + right border █
            AnsiConsole.MarkupLine(
                $"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]{leftBorder}[/]" +
                $"{row}" +
                $"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]");
        }

        // Render bottom border: █ + X-coordinates (0-9, then █ for rest) + █
        string bottomBorder = "█";
        for (int x = 0; x < width; x++)
        {
            bottomBorder += (x < 10) ? x.ToString() : "█";
        }
        bottomBorder += "█";
        
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]{bottomBorder}[/]");

        RenderBottomData(mapRenderData);
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
}