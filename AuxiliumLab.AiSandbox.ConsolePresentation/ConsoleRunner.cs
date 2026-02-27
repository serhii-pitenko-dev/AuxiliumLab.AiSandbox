using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Map.Entities;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetAffectedCells;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetMapLayout;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AuxiliumLab.AiSandbox.Common.MessageBroker;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;
using AuxiliumLab.AiSandbox.ConsolePresentation.Settings;
using AuxiliumLab.AiSandbox.Infrastructure.FileManager;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace AuxiliumLab.AiSandbox.ConsolePresentation;

public class ConsoleRunner : IConsoleRunner
{
    private readonly IMessageBroker _messageBroker;
    private readonly IMapQueriesHandleService _mapQueries;
    private readonly ConsoleSize _consoleSize;
    private readonly ColorScheme _consoleColorScheme;
    private readonly int _actionTimeout;
    private Dictionary<ObjectType, string> _cellData = [];
    private Guid _playgroundId;
    private int _mapRenderStartRow = 7; // Row where map rendering starts
    private int _mapWidth;
    private int _mapHeight;
    private List<string> _eventMessages = [];
    public event Action<Guid>? ReadyForRendering;
    private MapLayoutResponse _fullMapLayout;
    private readonly IFileDataManager<StandardPlaygroundState> _playgroundStateFileRepository;
    private StandardPlaygroundState _standardPlaygroundState;

    public ConsoleRunner(
        IAiActions aiActions,
        IMessageBroker messageBroker,
        IMapQueriesHandleService mapQueries,
        IOptions<ConsoleSettings> consoleSettings,
        IFileDataManager<StandardPlaygroundState> playgroundStateFileRepository)
    {
        _messageBroker = messageBroker;
        _mapQueries = mapQueries;
        _consoleSize = consoleSettings.Value.ConsoleSize;
        _consoleColorScheme = consoleSettings.Value.ColorScheme;
        _actionTimeout = consoleSettings.Value.ActionTimeout;
        _fullMapLayout = new MapLayoutResponse(-1, new MapCell[0, 0]);
        _playgroundStateFileRepository = playgroundStateFileRepository;
    }

    public void Run()
    {
        // Initialize console
        InitializeConsole();

        // Subscribe to message broker
        _messageBroker.Subscribe<GameStartedEvent>(OnGameStarted);
        _messageBroker.Subscribe<OnBaseAgentActionEvent>(OnAgentActionEvent);
        _messageBroker.Subscribe<TurnExecutedEvent>(OnTurnEnded);
        _messageBroker.Subscribe<HeroLostEvent>(OnGameLost);
        _messageBroker.Subscribe<HeroWonEvent>(OnGameWon);


        // Cleanup
        //_messageBroker.Unsubscribe<GameStartedEvent>(OnGameStarted);
        //_messageBroker.Unsubscribe<TurnExecutedEvent>(OnTurnEnded);
        //_messageBroker.Unsubscribe<HeroLostEvent>(OnGameLost);
        //_messageBroker.Unsubscribe<HeroWonEvent>(OnGameWon);
        //_messageBroker.Unsubscribe<OnBaseAgentActionEvent>(OnAgentActionEvent);

        //Console.ReadLine();
    }

    private void InitializeConsole()
    {
        Console.CursorVisible = false; // Hide the cursor
        InitializeElementsRendering();
        ResizeConsole(_consoleSize.Width, _consoleSize.Height);
        RenderBackground(_consoleColorScheme.GlobalBackGroundColor);
    }

    private void OnGameStarted(GameStartedEvent message)
    {
        _playgroundId = message.PlaygroundId;
        _standardPlaygroundState = _playgroundStateFileRepository.LoadObjectAsync(_playgroundId).Result;
        RenderInitialGameInfo();

        // Render the full map at game beginning
        _fullMapLayout = _mapQueries.MapLayoutQuery.GetFromMemory(_playgroundId);
        _mapWidth = _fullMapLayout.Cells.GetLength(0);
        _mapHeight = _fullMapLayout.Cells.GetLength(1);

        RenderFullMap(_fullMapLayout);
    }

    private void RerenderCells(HashSet<Coordinates> coordinates)
    {
        foreach (var coord in coordinates)
        {
            RerenderSingleCell(coord);
        }
    }

    private void RerenderSingleCell(Coordinates coordinates)
    {
        int x = coordinates.X;
        int y = coordinates.Y;

        // Get the cell from the full map layout
        MapCell cell = _fullMapLayout.Cells[x, y];

        // Calculate console position
        // Y is inverted: Cartesian Y 0 is at the bottom of the map
        int consoleX = x + 2; // +2 for left border and numbering
        int consoleY = _mapRenderStartRow + 1 + (_mapHeight - 1 - y); // +1 for top border

        // Set cursor position and render the cell
        Console.SetCursorPosition(consoleX, consoleY);
        AnsiConsole.Markup(GetCellSymbol(cell));
    }

    private void OnTurnEnded(TurnExecutedEvent message)
    {
        // Add turn increment message to event log
        _eventMessages.Add($"=== Turn {message.TurnNumber} completed ===");

        // Update turn number display
        Console.SetCursorPosition(0, 6);
        WriteSysInfoLine($" Turn: {message.TurnNumber}");

        RenderBottomData();
    }

    private void OnAgentActionEvent(OnBaseAgentActionEvent message)
    {

        string eventMessage = ConvertEventToString(message);
        _eventMessages.Add(eventMessage);
        RenderBottomData();

        // Handle cell updates based on event type
        if (message is OnAgentMoveActionEvent moveEvent)
        {
            HandleAgentMoveEvent(moveEvent);
        }

        // Wait for the configured turn timeout
        Thread.Sleep(_actionTimeout);
    }

    private void HandleAgentMoveEvent(OnAgentMoveActionEvent moveEvent)
    {
        var affectedCellsToRerender = new HashSet<Coordinates>();

        // Step 1: Remove old AgentEffect entries for this agent from all cells
        for (int x = 0; x < _mapWidth; x++)
        {
            for (int y = 0; y < _mapHeight; y++)
            {
                MapCell cell = _fullMapLayout.Cells[x, y];

                // Check if this cell has effects from the moving agent
                var effectsWithoutAgent = cell.Effects
                    .Where(effect => effect.AgentId != moveEvent.AgentId)
                    .ToArray();

                // If effects changed, update the cell
                if (effectsWithoutAgent.Length != cell.Effects.Length)
                {
                    _fullMapLayout.Cells[x, y] = cell with { Effects = effectsWithoutAgent };
                    affectedCellsToRerender.Add(new Coordinates(x, y));
                }
            }
        }

        // Step 2: Get new affected cells for this agent
        AffectedCellsResponse affectedCellsResponse =
            _mapQueries.AffectedCellsQuery.GetFromMemory(_playgroundId, moveEvent.AgentId);

        // Step 3: Apply new AgentEffect entries to the map
        foreach (var newCell in affectedCellsResponse.Cells)
        {
            int x = newCell.Coordinates.X;
            int y = newCell.Coordinates.Y;

            // Get the current cell from the full map layout
            MapCell currentCell = _fullMapLayout.Cells[x, y];

            // Merge effects: keep existing effects from other agents and add new effects
            var mergedEffects = currentCell.Effects
                .Where(effect => effect.AgentId != moveEvent.AgentId) // Remove old effects for this agent (safety)
                .Concat(newCell.Effects) // Add new effects
                .ToArray();

            // Update the cell in the full map layout
            _fullMapLayout.Cells[x, y] = currentCell with { Effects = mergedEffects };
            affectedCellsToRerender.Add(newCell.Coordinates);
        }

        // Step 4: Apply move result to local map state.
        // Uses ConsoleMapState.ApplyAgentMove which handles the IsSuccess guard:
        // successful → clear "from", stamp "to"; failed → only queue "from" for
        // re-render so the agent icon is never erased from its real position.
        ConsoleMapState.ApplyAgentMove(
            _fullMapLayout.Cells,
            moveEvent.From,
            moveEvent.To,
            moveEvent.AgentId,
            moveEvent.IsSuccess,
            affectedCellsToRerender);

        // Step 5: Re-render all affected cells
        RerenderCells(affectedCellsToRerender);
    }

    private string ConvertEventToString(OnBaseAgentActionEvent actionEvent)
    {
        string eventMessage = actionEvent switch
        {
            OnAgentMoveActionEvent moveEvent =>
                moveEvent.IsSuccess
                    ? $"Agent {moveEvent.AgentId:N} moved from ({moveEvent.From.X}, {moveEvent.From.Y}) to ({moveEvent.To.X}, {moveEvent.To.Y})"
                    : $"Agent {moveEvent.AgentId:N} FAILED to move from ({moveEvent.From.X}, {moveEvent.From.Y}) - invalid move",
            OnAgentToggleActionEvent toggleActionEvent =>
                    $"Agent {toggleActionEvent.AgentId:N} {(toggleActionEvent.IsActivated ? "activated" : "deactivated")} action: {toggleActionEvent.AgentAction}",
                    
            _ => $"Unknown event: {actionEvent.GetType().Name}"
        };


        string runStatus = actionEvent.AgentSnapshot.IsRun ? "Running" : "Walking";
        eventMessage += $"\n    → Type: {actionEvent.AgentSnapshot.Type}, Speed: {actionEvent.AgentSnapshot.Speed}, Sight: {actionEvent.AgentSnapshot.SightRange}, {runStatus}, Stamina: {actionEvent.AgentSnapshot.Stamina}, Max Stamina: {actionEvent.AgentSnapshot.MaxStamina}, Turn Order: {actionEvent.AgentSnapshot.OrderInTurnQueue}";


        return eventMessage;
    }

    private void RenderInitialGameInfo()
    {

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        WriteSysInfoLine($"Map initialized with ID: {_playgroundId}");
        WriteSysInfoLine($"Width: {_standardPlaygroundState.Map.Width}; Height: {_standardPlaygroundState.Map.Height}; Area: {_standardPlaygroundState.Map.Width * _standardPlaygroundState.Map.Height}; Percent of blocks: {_standardPlaygroundState.Blocks.Count / (double)(_standardPlaygroundState.Map.Width * _standardPlaygroundState.Map.Height):P}; Percent of enemies {_standardPlaygroundState.Enemies.Count / (double)(_standardPlaygroundState.Map.Width * _standardPlaygroundState.Map.Height):P}");
        WriteSysInfoLine($"Initial elements count: blocks - {_standardPlaygroundState.Blocks.Count}; enemy - {_standardPlaygroundState.Enemies.Count}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private void RenderFullMap(MapLayoutResponse mapRenderData)
    {
        int width = mapRenderData.Cells.GetLength(0);
        int height = mapRenderData.Cells.GetLength(1);

        Console.SetCursorPosition(0, 6);
        WriteSysInfoLine($" Turn: {mapRenderData.turnNumber}");

        // Render top border
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}] {new string('█', width + 2)}[/]");

        // Render rows: iterate Cartesian Y from top to bottom
        for (int cartesianY = height - 1; cartesianY >= 0; cartesianY--)
        {
            string row = string.Empty;

            for (int x = 0; x < width; x++)
            {
                MapCell cell = mapRenderData.Cells[x, cartesianY];
                row += GetCellSymbol(cell);
            }

            string leftBorder = (cartesianY < 10) ? cartesianY.ToString() : "█";

            AnsiConsole.MarkupLine(
                $"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}] {leftBorder}[/]" +
                $"{row}" +
                $"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]");
        }

        // Render bottom border
        string bottomBorder = " █";
        for (int x = 0; x < width; x++)
        {
            bottomBorder += (x < 10) ? x.ToString() : "█";
        }
        bottomBorder += "█";

        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.MapBackGroundColor}]{bottomBorder}[/]");

        RenderBottomData();
    }

    private string GetCellSymbol(MapCell cell)
    {
        // First check if there's an actual agent/object at this cell
        if (cell.ObjectType != ObjectType.Empty)
        {
            return _cellData[cell.ObjectType];
        }

        // Priority order for rendering effects:
        // 1. Hero Path (highest priority)
        // 2. Hero Vision
        // 3. Enemy Path
        // 4. Enemy Vision (lowest priority)

        bool hasHeroPath = false;
        bool hasHeroVision = false;
        bool hasEnemyPath = false;
        bool hasEnemyVision = false;

        foreach (var agentEffect in cell.Effects)
        {
            if (agentEffect.AgentType == ObjectType.Hero)
            {
                if (agentEffect.Effects.Contains(EEffect.Path))
                    hasHeroPath = true;
                if (agentEffect.Effects.Contains(EEffect.Vision))
                    hasHeroVision = true;
            }
            else if (agentEffect.AgentType == ObjectType.Enemy)
            {
                if (agentEffect.Effects.Contains(EEffect.Path))
                    hasEnemyPath = true;
                if (agentEffect.Effects.Contains(EEffect.Vision))
                    hasEnemyVision = true;
            }
        }

        // Render based on priority
        if (hasHeroPath)
            return $"[#000000 on {_consoleColorScheme.HeroPathColor}]·[/]";
        if (hasHeroVision)
            return $"[#000000 on {_consoleColorScheme.HeroVisionColor}]·[/]";
        if (hasEnemyPath)
            return $"[#000000 on {_consoleColorScheme.EnemyPathColor}]·[/]";
        if (hasEnemyVision)
            return $"[#000000 on {_consoleColorScheme.EnemyVisionColor}]·[/]";

        return _cellData[ObjectType.Empty];
    }

    private void RenderBottomData()
    {
        // Calculate the starting row for bottom data (after the map)
        int bottomDataStartRow = _mapRenderStartRow + _mapHeight + 3;

        Console.SetCursorPosition(0, bottomDataStartRow);

        // Clear the bottom area with empty lines
        for (int i = 0; i < 21; i++)
        {
            AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.GlobalBackGroundColor}]{new string(' ', Console.WindowWidth)}[/]");
        }

        // Render event messages
        Console.SetCursorPosition(0, bottomDataStartRow);
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.GlobalBackGroundColor}]Events:[/]");

        // Display the last 15 events (FIFO)
        int eventsToShow = Math.Min(_eventMessages.Count, 15);
        for (int i = _eventMessages.Count - eventsToShow; i < _eventMessages.Count; i++)
        {
            AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.GlobalBackGroundColor}]  {_eventMessages[i]}[/]");
        }
    }

    private void WriteSysInfoLine(string message)
    {
        AnsiConsole.MarkupLine($"[{_consoleColorScheme.BorderColor} on {_consoleColorScheme.GlobalBackGroundColor}]{message}[/]");
    }

    private void InitializeElementsRendering()
    {
        _cellData = new Dictionary<ObjectType, string>
        {
            { ObjectType.Empty, $"[#000000 on {_consoleColorScheme.MapBackGroundColor}]·[/]" },
            { ObjectType.Block, $"[{_consoleColorScheme.BlockColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]" },
            { ObjectType.BorderBlock, $"[{_consoleColorScheme.BorderBlockColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]" },
            { ObjectType.Hero, $"[{_consoleColorScheme.HeroColor} on {_consoleColorScheme.MapBackGroundColor}]█[/]" },
            { ObjectType.Enemy, $"[{_consoleColorScheme.EnemyColor} on {_consoleColorScheme.MapBackGroundColor}]X[/]" },
            { ObjectType.Exit, $"[Black on Green]E[/]" }
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

    private void OnGameWon(HeroWonEvent gameWonEventMessage)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[Green on {_consoleColorScheme.GlobalBackGroundColor}]!!! HERO WIN !!![/]");
    }

    private void OnGameLost(HeroLostEvent gameLostEventMessage)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[Red on {_consoleColorScheme.GlobalBackGroundColor}]!!! HERO LOST !!![/]");
    }

    private void OnTurnLimitReached()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[Red on {_consoleColorScheme.GlobalBackGroundColor}]!!! TURN LIMIT REACHED !!![/]");
    }
}