using AiSandBox.ApplicationServices.Executors;
using AiSandBox.Domain.Playgrounds.Factories;
using AiSandBox.Domain.Statistics.Result;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Runner.MassRunner;

/// <summary>
/// Handles parallel batch runs and incremental sweep runs.
/// </summary>
public class MassRunner
{
    private readonly IFileDataManager<GeneralBatchRunInformation> _batchResultFileManager;
    private readonly SandBoxConfiguration _configuration;

    public MassRunner(
        IFileDataManager<GeneralBatchRunInformation> batchResultFileManager,
        IOptions<SandBoxConfiguration> configuration)
    {
        _batchResultFileManager = batchResultFileManager;
        _configuration = configuration.Value;
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
        IEnumerable<string>? incrementalProperties = null)
    {
        var startTime = DateTime.Now;
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"MASS RUNNER - Starting batch execution at {startTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        
        configuration ??= _configuration;
        var propertiesToSweep = incrementalProperties?.ToList() ?? new List<string>();

        var batchRunId = Guid.NewGuid();
        Console.WriteLine($"Batch Run ID: {batchRunId}");
        Console.WriteLine($"Standard parallel runs: {count}");
        Console.WriteLine($"Max parallelism: {Environment.ProcessorCount} threads");
        if (propertiesToSweep.Any())
        {
            Console.WriteLine($"Incremental properties to sweep: {string.Join(", ", propertiesToSweep)}");
        }
        Console.WriteLine();
        
        int wins = 0;
        int completedRuns = 0;
        long totalTurns = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        int area = (int)configuration.MapSettings.Size.Height * (int)configuration.MapSettings.Size.Width;
        GeneralBatchRunInformation batchRunInfo = new GeneralBatchRunInformation(
            BlocksCount: PlaygroundFactory.PercentCalculation(area, configuration.MapSettings.ElementsPercentages.BlocksPercent),
            EnemiesCount: PlaygroundFactory.PercentCalculation(area, configuration.MapSettings.ElementsPercentages.PercentOfEnemies),
            Area: area,
            MapWidth: configuration.MapSettings.Size.Width,
            MapHeight: configuration.MapSettings.Size.Height,
            MapArea: area
        );

        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Map Size: {configuration.MapSettings.Size.Width} × {configuration.MapSettings.Size.Height} (Area: {area})");
        Console.WriteLine($"  Blocks: {batchRunInfo.BlocksCount}, Enemies: {batchRunInfo.EnemiesCount}");
        Console.WriteLine($"  Max Turns: {configuration.MaxTurns.Current}");
        Console.WriteLine();

        await _batchResultFileManager.AppendObjectAsync(batchRunId, batchRunInfo);

        // ── Standard parallel runs ─────────────────────────────────────────────
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting {count} standard parallel runs...");
        var standardRunsStartTime = DateTime.Now;
        
        await Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            options,
            async (_, _) =>
            {
                var result = await executorFactory.CreateStandardExecutor().RunAndCaptureAsync(configuration);

                await _batchResultFileManager.AppendObjectAsync(batchRunId, result);

                Interlocked.Increment(ref completedRuns);
                Interlocked.Add(ref totalTurns, result.TurnsCount);
                if (result.WinReason.HasValue)
                    Interlocked.Increment(ref wins);
            });

        var standardRunsDuration = DateTime.Now - standardRunsStartTime;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed {count} standard runs in {standardRunsDuration.TotalSeconds:F2}s");
        Console.WriteLine($"  Progress: {completedRuns} runs completed, {wins} wins");
        Console.WriteLine();

        // ── Incremental sweep runs ─────────────────────────────────────────────
        bool areaEnabled = configuration.MapSettings.Size.IncrementalArea?.IsEnabled == true;

        if (propertiesToSweep.Any())
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting incremental sweep runs...");
        }

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
            Console.WriteLine($"  Sweeping '{propertyName}': {range.Min} to {range.Max}, step {range.Step} ({stepCount} runs)");
            
            for (int i = 0; i < stepCount; i++)
            {
                int currentValue = range.Min + i * range.Step;
                var overriddenConfig = WithPropertyOverride(configuration, propertyName, currentValue);

                var result = await executorFactory.CreateStandardExecutor().RunAndCaptureAsync(overriddenConfig);
                await _batchResultFileManager.AppendObjectAsync(batchRunId, result);

                Interlocked.Increment(ref completedRuns);
                Interlocked.Add(ref totalTurns, result.TurnsCount);
                if (result.WinReason.HasValue)
                    Interlocked.Increment(ref wins);
            }
            
            Console.WriteLine($"    Completed {stepCount} runs for '{propertyName}'");
        }

        // ── Joint area sweep (Width + Height stepped together) ─────────────────
        if (areaEnabled)
        {
            var sz = configuration.MapSettings.Size;
            int areaStep = configuration.MapSettings.Size.IncrementalArea!.Step;

            int widthRange  = sz.Width.Max  - sz.Width.Min;
            int heightRange = sz.Height.Max - sz.Height.Min;
            int biggerRange = Math.Max(widthRange, heightRange);
            int iterations  = areaStep > 0 ? biggerRange / areaStep : 0;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting joint area sweep (Width + Height)...");
            Console.WriteLine($"  Width range: {sz.Width.Min} to {sz.Width.Max}");
            Console.WriteLine($"  Height range: {sz.Height.Min} to {sz.Height.Max}");
            Console.WriteLine($"  Step: {areaStep}, Iterations: {iterations}");

            for (int i = 0; i < iterations; i++)
            {
                int wValue = Math.Min(sz.Width.Min  + i * areaStep, sz.Width.Max);
                int hValue = Math.Min(sz.Height.Min + i * areaStep, sz.Height.Max);

                var overriddenConfig = WithAreaOverride(configuration, wValue, hValue);

                var result = await executorFactory.CreateStandardExecutor().RunAndCaptureAsync(overriddenConfig);
                await _batchResultFileManager.AppendObjectAsync(batchRunId, result);

                Interlocked.Increment(ref completedRuns);
                Interlocked.Add(ref totalTurns, result.TurnsCount);
                if (result.WinReason.HasValue)
                    Interlocked.Increment(ref wins);
            }
            
            Console.WriteLine($"  Completed {iterations} area sweep runs");
            Console.WriteLine();
        }

        // ── Summary ────────────────────────────────────────────────────────────
        int losses = completedRuns - wins;
        double avgTurns = completedRuns > 0 ? (double)totalTurns / completedRuns : 0;
        await _batchResultFileManager.AppendObjectAsync(batchRunId, new BatchSummary(completedRuns, wins, losses, avgTurns));
        
        var finishTime = DateTime.Now;
        var totalDuration = finishTime - startTime;
        
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"MASS RUNNER - Batch execution completed at {finishTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"Total Duration: {totalDuration.TotalSeconds:F2}s ({totalDuration:hh\\:mm\\:ss})");
        Console.WriteLine($"Total Runs: {completedRuns}");
        Console.WriteLine($"Wins: {wins} ({(completedRuns > 0 ? (wins * 100.0 / completedRuns) : 0):F1}%)");
        Console.WriteLine($"Losses: {losses} ({(completedRuns > 0 ? (losses * 100.0 / completedRuns) : 0):F1}%)");
        Console.WriteLine($"Average Turns: {avgTurns:F2}");
        Console.WriteLine($"Total Turns: {totalTurns:N0}");
        Console.WriteLine($"Average Duration per Run: {(completedRuns > 0 ? totalDuration.TotalSeconds / completedRuns : 0):F3}s");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
