using AiSandBox.ApplicationServices.Executors;
using AiSandBox.Domain.Playgrounds.Factories;
using AiSandBox.Domain.Statistics.Result;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Statistics.Converters;
using AiSandBox.Statistics.Preconditions;
using AiSandBox.Statistics.StatisticDataManager;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Runner.MassRunner;

/// <summary>
/// Handles parallel batch runs and incremental sweep runs.
/// </summary>
public class MassRunner
{
    private readonly IStatisticFileDataManager _statisticFileManager;
    private readonly SandBoxConfiguration _configuration;

    public MassRunner(
        IFileDataManager<GeneralBatchRunInformation> batchResultFileManager,
        IStatisticFileDataManager statisticFileManager,
        IOptions<SandBoxConfiguration> configuration)
    {
        _statisticFileManager   = statisticFileManager;
        _configuration          = configuration.Value;
    }

    /// <summary>
    /// Runs <paramref name="count"/> standard simulations in parallel and, for every entry in
    /// <paramref name="incrementalProperties"/>, an additional sweep of simulations – one per step
    /// value of the corresponding <see cref="IncrementalRange"/> – all at their standard values
    /// except for the swept property.
    /// <para>
    /// Total runs = <paramref name="count"/> + Σ ( (Max − Min) / Step ) for each swept property.
    /// </para>
    /// </summary>
    /// <param name="executor">The executor used to run each simulation.</param>
    /// <param name="count">Number of standard (baseline) runs.</param>
    /// <param name="configuration">
    ///   Configuration to use. Falls back to the injected default when <see langword="null"/>.
    /// </param>
    /// <param name="incrementalProperties">
    ///   Property names from <see cref="IncrementalPropertyNames"/> whose ranges should be swept.
    ///   Pass <see langword="null"/> or an empty enumerable to skip incremental runs entirely.
    /// </param>
    public async Task RunManyAsync(
        IExecutorFactory executorFactory,
        int count,
        SandBoxConfiguration? configuration = null,
        SimulationStartupSettings? startupSettings = null)
    {
        var startTime = DateTime.Now;
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"MASS RUNNER - Starting batch execution at {startTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");

        configuration ??= _configuration;
        var propertiesToSweep      = startupSettings?.IncrementalProperties.Properties ?? [];
        int incrementalSimCount    = startupSettings?.IncrementalProperties.SimulationCount ?? 1;
        var batchRunId = Guid.NewGuid();
        bool areaEnabled = configuration.MapSettings.Size.IncrementalArea?.IsEnabled == true;

        LogBatchHeader(batchRunId, count, propertiesToSweep, incrementalSimCount);

        string csvFileName = $"{batchRunId}.csv";
        await SavePreconditionsCsvAsync(configuration, startupSettings, csvFileName);
        await SaveBatchRunInfoAsync(configuration, batchRunId);

        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        var standardResult  = await RunStandardPhaseAsync(executorFactory, configuration, batchRunId, count, options);
        var sweepResults    = await RunIncrementalSweepPhaseAsync(executorFactory, configuration, batchRunId, propertiesToSweep, areaEnabled, incrementalSimCount);
        var areaSweepResult = areaEnabled
            ? await RunAreaSweepPhaseAsync(executorFactory, configuration, batchRunId)
            : null;

        // Build one BatchSummary per executed phase (skip empty area sweep slot)
        var allPhaseResults = new List<PhaseResult> { standardResult };
        allPhaseResults.AddRange(sweepResults);
        if (areaSweepResult is not null)
            allPhaseResults.Add(areaSweepResult);

        var batchSummaries = allPhaseResults
            .Select(r => new BatchSummary(
                Guid.NewGuid(),
                r.Completed,
                r.Wins,
                r.Completed - r.Wins,
                r.Completed > 0 ? (double)r.TotalTurns / r.Completed : 0,
                r.Description))
            .ToList();

        int totalWins      = allPhaseResults.Sum(r => r.Wins);
        int totalCompleted = allPhaseResults.Sum(r => r.Completed);
        long totalTurns    = allPhaseResults.Sum(r => r.TotalTurns);

        var massRunSummary = new MassRunSummary(
            batchSummaries.Count,
            DateTime.Now - startTime,
            string.Join("; ", batchSummaries.Select(b => b.Description)));

        await AppendBatchSummariesCsvAsync(csvFileName, batchSummaries);
        await AppendMassRunSummaryCsvAsync(csvFileName, massRunSummary);

        LogBatchFooter(startTime, totalCompleted, totalWins, totalTurns);
    }

    // ── Step 1: console header ─────────────────────────────────────────────────

    private static void LogBatchHeader(Guid batchRunId, int count, List<string> propertiesToSweep, int incrementalSimulationCount)
    {
        Console.WriteLine($"Batch Run ID: {batchRunId}");
        Console.WriteLine($"Standard parallel runs: {count}");
        Console.WriteLine($"Max parallelism: {Environment.ProcessorCount} threads");
        if (propertiesToSweep.Any())
        {
            Console.WriteLine($"Incremental properties to sweep: {string.Join(", ", propertiesToSweep)}");
            Console.WriteLine($"Incremental simulation count per step: {incrementalSimulationCount}");
        }
        Console.WriteLine();
    }

    // ── Step 2: save preconditions CSV ────────────────────────────────────────

    private async Task SavePreconditionsCsvAsync(
        SandBoxConfiguration configuration,
        SimulationStartupSettings? startupSettings,
        string csvFileName)
    {
        if (startupSettings is not null)
        {
            await _statisticFileManager.ConvertToCsvAndAppendAsync(csvFileName, TableConverter.ToCsv(startupSettings));
            await _statisticFileManager.ConvertToCsvAndAppendAsync(csvFileName, Environment.NewLine);
        }

        await _statisticFileManager.ConvertToCsvAndAppendAsync(csvFileName, TableConverter.ToCsv(BuildSandBoxSettings(configuration)));
        await _statisticFileManager.ConvertToCsvAndAppendAsync(csvFileName, Environment.NewLine);
    }

    // ── Step 3: persist batch run info ────────────────────────────────────────

    private async Task SaveBatchRunInfoAsync(SandBoxConfiguration configuration, Guid batchRunId)
    {
        int area = (int)configuration.MapSettings.Size.Height * (int)configuration.MapSettings.Size.Width;
        var batchRunInfo = new GeneralBatchRunInformation(
            BlocksCount:  PlaygroundFactory.PercentCalculation(area, configuration.MapSettings.ElementsPercentages.BlocksPercent),
            EnemiesCount: PlaygroundFactory.PercentCalculation(area, configuration.MapSettings.ElementsPercentages.PercentOfEnemies),
            Area:         area,
            MapWidth:     configuration.MapSettings.Size.Width,
            MapHeight:    configuration.MapSettings.Size.Height,
            MapArea:      area);

        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Map Size: {configuration.MapSettings.Size.Width} × {configuration.MapSettings.Size.Height} (Area: {area})");
        Console.WriteLine($"  Blocks: {batchRunInfo.BlocksCount}, Enemies: {batchRunInfo.EnemiesCount}");
        Console.WriteLine($"  Max Turns: {configuration.MaxTurns.Current}");
        Console.WriteLine();
    }

    // ── Step 4: standard parallel runs ────────────────────────────────────────

    private async Task<PhaseResult> RunStandardPhaseAsync(
        IExecutorFactory executorFactory,
        SandBoxConfiguration configuration,
        Guid batchRunId,
        int count,
        ParallelOptions options)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting {count} standard parallel runs...");
        var phaseStart = DateTime.Now;

        int wins = 0, completed = 0;
        long totalTurns = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            options,
            async (_, _) =>
            {
                var result = await executorFactory.CreateStandardExecutor().RunAndCaptureAsync(configuration);

                Interlocked.Increment(ref completed);
                Interlocked.Add(ref totalTurns, result.TurnsCount);
                if (result.WinReason.HasValue)
                    Interlocked.Increment(ref wins);
            });

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed {count} standard runs in {(DateTime.Now - phaseStart).TotalSeconds:F2}s");
        Console.WriteLine($"  Progress: {completed} runs completed, {wins} wins");
        Console.WriteLine();

        return new PhaseResult(wins, completed, totalTurns, $"Standard parallel runs ({count} runs)");
    }

    // ── Step 5: incremental named-property sweep ───────────────────────────────

    private async Task<IReadOnlyList<PhaseResult>> RunIncrementalSweepPhaseAsync(
        IExecutorFactory executorFactory,
        SandBoxConfiguration configuration,
        Guid batchRunId,
        List<string> propertiesToSweep,
        bool areaEnabled,
        int simulationCount)
    {
        if (!propertiesToSweep.Any())
            return [];

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting incremental sweep runs...");

        var results = new List<PhaseResult>();

        foreach (var propertyName in propertiesToSweep)
        {
            // Skip individual Width/Height sweeps when joint area sweep is active
            if (areaEnabled &&
                (propertyName == IncrementalPropertyNames.MapWidth ||
                 propertyName == IncrementalPropertyNames.MapHeight))
                continue;

            var range = GetRange(configuration, propertyName);
            if (range is null)
                continue;

            int stepCount = range.StepCount;
            Console.WriteLine($"  Sweeping '{propertyName}': {range.Min} to {range.Max}, step {range.Step} ({stepCount} steps × {simulationCount} runs each = {stepCount * simulationCount} total)");

            for (int i = 0; i < stepCount; i++)
            {
                int currentValue    = range.Min + i * range.Step;
                var overriddenConfig = WithPropertyOverride(configuration, propertyName, currentValue);

                int stepWins = 0, stepCompleted = 0;
                long stepTurns = 0;

                for (int s = 0; s < simulationCount; s++)
                {
                    var result = await executorFactory.CreateStandardExecutor().RunAndCaptureAsync(overriddenConfig);
                    stepCompleted++;
                    stepTurns += result.TurnsCount;
                    if (result.WinReason.HasValue)
                        stepWins++;
                }

                results.Add(new PhaseResult(stepWins, stepCompleted, stepTurns,
                    $"Sweep '{propertyName}'={currentValue} ({simulationCount} runs)"));
            }

            Console.WriteLine($"    Completed {stepCount * simulationCount} runs for '{propertyName}' ({stepCount} steps)");
        }

        return results;
    }

    // ── Step 6: joint area sweep (Width + Height together) ────────────────────

    private async Task<PhaseResult> RunAreaSweepPhaseAsync(
        IExecutorFactory executorFactory,
        SandBoxConfiguration configuration,
        Guid batchRunId)
    {
        var sz       = configuration.MapSettings.Size;
        int areaStep = configuration.MapSettings.Size.IncrementalArea!.Step;

        int biggerRange = Math.Max(sz.Width.Max - sz.Width.Min, sz.Height.Max - sz.Height.Min);
        int iterations  = areaStep > 0 ? biggerRange / areaStep : 0;

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting joint area sweep (Width + Height)...");
        Console.WriteLine($"  Width range: {sz.Width.Min} to {sz.Width.Max}");
        Console.WriteLine($"  Height range: {sz.Height.Min} to {sz.Height.Max}");
        Console.WriteLine($"  Step: {areaStep}, Iterations: {iterations}");

        int wins = 0, completed = 0;
        long totalTurns = 0;

        for (int i = 0; i < iterations; i++)
        {
            int wValue = Math.Min(sz.Width.Min  + i * areaStep, sz.Width.Max);
            int hValue = Math.Min(sz.Height.Min + i * areaStep, sz.Height.Max);

            var result = await executorFactory.CreateStandardExecutor().RunAndCaptureAsync(WithAreaOverride(configuration, wValue, hValue));

            completed++;
            totalTurns += result.TurnsCount;
            if (result.WinReason.HasValue)
                wins++;
        }

        Console.WriteLine($"  Completed {iterations} area sweep runs");
        Console.WriteLine();

        return new PhaseResult(wins, completed, totalTurns,
            $"Joint area sweep (Width {sz.Width.Min}→{sz.Width.Max}, Height {sz.Height.Min}→{sz.Height.Max}, step {areaStep}, {iterations} runs)");
    }

    // ── Step 7: persist and CSV-export batch summaries ────────────────────────

    private async Task AppendBatchSummariesCsvAsync(string csvFileName, IList<BatchSummary> batchSummaries)
    {
        await _statisticFileManager.ConvertToCsvAndAppendAsync(csvFileName, TableConverter.ToCsv(batchSummaries));
        await _statisticFileManager.ConvertToCsvAndAppendAsync(csvFileName, Environment.NewLine);
    }

    private async Task AppendMassRunSummaryCsvAsync(string csvFileName, MassRunSummary massRunSummary)
    {
        await _statisticFileManager.ConvertToCsvAndAppendAsync(csvFileName, TableConverter.ToCsv(massRunSummary));
    }

    // ── Step 8: console footer ─────────────────────────────────────────────────

    private static void LogBatchFooter(DateTime startTime, int totalCompleted, int totalWins, long totalTurns)
    {
        int losses      = totalCompleted - totalWins;
        double avgTurns = totalCompleted > 0 ? (double)totalTurns / totalCompleted : 0;
        var finishTime  = DateTime.Now;
        var duration    = finishTime - startTime;

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"MASS RUNNER - Batch execution completed at {finishTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"Total Duration: {duration.TotalSeconds:F2}s ({duration:hh\\:mm\\:ss})");
        Console.WriteLine($"Total Runs: {totalCompleted}");
        Console.WriteLine($"Wins: {totalWins} ({(totalCompleted > 0 ? (totalWins * 100.0 / totalCompleted) : 0):F1}%)");
        Console.WriteLine($"Losses: {losses} ({(totalCompleted > 0 ? (losses * 100.0 / totalCompleted) : 0):F1}%)");
        Console.WriteLine($"Average Turns: {avgTurns:F2}");
        Console.WriteLine($"Total Turns: {totalTurns:N0}");
        Console.WriteLine($"Average Duration per Run: {(totalCompleted > 0 ? duration.TotalSeconds / totalCompleted : 0):F3}s");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps the relevant fields of <paramref name="cfg"/> to a <see cref="SimulationSandBoxSettings"/>
    /// for CSV export, omitting TotalRuns, SaveToFileRegularity, TurnTimeout, FileSource and Type.
    /// </summary>
    private static SimulationSandBoxSettings BuildSandBoxSettings(SandBoxConfiguration cfg)
    {
        static RangeSettings Map(IncrementalRange r) => new(r.Min, r.Current, r.Max, r.Step);

        return new SimulationSandBoxSettings
        {
            MaxTurns              = Map(cfg.MaxTurns),
            MapWidth              = Map(cfg.MapSettings.Size.Width),
            MapHeight             = Map(cfg.MapSettings.Size.Height),
            IncrementalAreaEnabled = cfg.MapSettings.Size.IncrementalArea?.IsEnabled ?? false,
            IncrementalAreaStep    = cfg.MapSettings.Size.IncrementalArea?.Step ?? 0,
            BlocksPercent         = Map(cfg.MapSettings.ElementsPercentages.BlocksPercent),
            PercentOfEnemies      = Map(cfg.MapSettings.ElementsPercentages.PercentOfEnemies),
            HeroSpeed             = Map(cfg.Hero.Speed),
            HeroSightRange        = Map(cfg.Hero.SightRange),
            HeroStamina           = Map(cfg.Hero.Stamina),
            EnemySpeed            = Map(cfg.Enemy.Speed),
            EnemySightRange       = Map(cfg.Enemy.SightRange),
            EnemyStamina          = Map(cfg.Enemy.Stamina),
        };
    }

    /// <summary>
    /// Returns the <see cref="IncrementalRange"/> for the given property name, or <see langword="null"/>
    /// if the name is not recognised.
    /// </summary>
    private static IncrementalRange? GetRange(SandBoxConfiguration cfg, string propertyName)
        => propertyName switch
        {
            IncrementalPropertyNames.MaxTurns        => cfg.MaxTurns,
            IncrementalPropertyNames.MapWidth         => cfg.MapSettings.Size.Width,
            IncrementalPropertyNames.MapHeight        => cfg.MapSettings.Size.Height,
            IncrementalPropertyNames.BlocksPercent    => cfg.MapSettings.ElementsPercentages.BlocksPercent,
            IncrementalPropertyNames.PercentOfEnemies => cfg.MapSettings.ElementsPercentages.PercentOfEnemies,
            IncrementalPropertyNames.HeroSpeed        => cfg.Hero.Speed,
            IncrementalPropertyNames.HeroSightRange   => cfg.Hero.SightRange,
            IncrementalPropertyNames.HeroStamina      => cfg.Hero.Stamina,
            IncrementalPropertyNames.EnemySpeed       => cfg.Enemy.Speed,
            IncrementalPropertyNames.EnemySightRange  => cfg.Enemy.SightRange,
            IncrementalPropertyNames.EnemyStamina     => cfg.Enemy.Stamina,
            _                                         => null
        };

    /// <summary>
    /// Returns a shallow copy of <paramref name="config"/> where the named property's
    /// <see cref="IncrementalRange.Current"/> is replaced with <paramref name="value"/>.
    /// All other property references remain unchanged.
    /// </summary>
    private static SandBoxConfiguration WithPropertyOverride(SandBoxConfiguration config, string propertyName, int value)
    {
        // Shallow-copy the top-level class
        var updated = new SandBoxConfiguration
        {
            MaxTurns            = config.MaxTurns,
            TurnTimeout         = config.TurnTimeout,
            SaveToFileRegularity= config.SaveToFileRegularity,
            IsDebugMode         = config.IsDebugMode,
            MapSettings         = config.MapSettings,
            Hero                = config.Hero,
            Enemy               = config.Enemy,
        };

        switch (propertyName)
        {
            case IncrementalPropertyNames.MaxTurns:
                updated.MaxTurns = config.MaxTurns.WithCurrent(value);
                break;

            case IncrementalPropertyNames.MapWidth:
            {
                var ms = updated.MapSettings;
                var sz = ms.Size;
                sz.Width = sz.Width.WithCurrent(value);
                ms.Size = sz;
                updated.MapSettings = ms;
                break;
            }

            case IncrementalPropertyNames.MapHeight:
            {
                var ms = updated.MapSettings;
                var sz = ms.Size;
                sz.Height = sz.Height.WithCurrent(value);
                ms.Size = sz;
                updated.MapSettings = ms;
                break;
            }

            case IncrementalPropertyNames.BlocksPercent:
            {
                var ms = updated.MapSettings;
                var ep = ms.ElementsPercentages;
                ep.BlocksPercent = ep.BlocksPercent.WithCurrent(value);
                ms.ElementsPercentages = ep;
                updated.MapSettings = ms;
                break;
            }

            case IncrementalPropertyNames.PercentOfEnemies:
            {
                var ms = updated.MapSettings;
                var ep = ms.ElementsPercentages;
                ep.PercentOfEnemies = ep.PercentOfEnemies.WithCurrent(value);
                ms.ElementsPercentages = ep;
                updated.MapSettings = ms;
                break;
            }

            case IncrementalPropertyNames.HeroSpeed:
            {
                var hero = updated.Hero;
                hero.Speed = hero.Speed.WithCurrent(value);
                updated.Hero = hero;
                break;
            }

            case IncrementalPropertyNames.HeroSightRange:
            {
                var hero = updated.Hero;
                hero.SightRange = hero.SightRange.WithCurrent(value);
                updated.Hero = hero;
                break;
            }

            case IncrementalPropertyNames.HeroStamina:
            {
                var hero = updated.Hero;
                hero.Stamina = hero.Stamina.WithCurrent(value);
                updated.Hero = hero;
                break;
            }

            case IncrementalPropertyNames.EnemySpeed:
            {
                var enemy = updated.Enemy;
                enemy.Speed = enemy.Speed.WithCurrent(value);
                updated.Enemy = enemy;
                break;
            }

            case IncrementalPropertyNames.EnemySightRange:
            {
                var enemy = updated.Enemy;
                enemy.SightRange = enemy.SightRange.WithCurrent(value);
                updated.Enemy = enemy;
                break;
            }

            case IncrementalPropertyNames.EnemyStamina:
            {
                var enemy = updated.Enemy;
                enemy.Stamina = enemy.Stamina.WithCurrent(value);
                updated.Enemy = enemy;
                break;
            }
        }

        return updated;
    }

    /// <summary>
    /// Returns a shallow copy of <paramref name="config"/> where both
    /// <see cref="IncrementalRange.Current"/> of Width and Height are overridden simultaneously,
    /// preserving the <see cref="IncrementalArea"/> settings unchanged.
    /// </summary>
    private static SandBoxConfiguration WithAreaOverride(SandBoxConfiguration config, int width, int height)
    {
        var updated = new SandBoxConfiguration
        {
            MaxTurns             = config.MaxTurns,
            TurnTimeout          = config.TurnTimeout,
            SaveToFileRegularity = config.SaveToFileRegularity,
            IsDebugMode          = config.IsDebugMode,
            MapSettings          = config.MapSettings,
            Hero                 = config.Hero,
            Enemy                = config.Enemy,
        };

        var ms = updated.MapSettings;
        var sz = ms.Size;
        sz.Width  = sz.Width.WithCurrent(width);
        sz.Height = sz.Height.WithCurrent(height);
        ms.Size   = sz;
        updated.MapSettings = ms;

        return updated;
    }
}

/// <summary>Aggregated run counts returned by each execution phase of <see cref="MassRunner"/>.</summary>
internal record PhaseResult(int Wins, int Completed, long TotalTurns, string Description)
{
    public static readonly PhaseResult Empty = new(0, 0, 0, string.Empty);
}
