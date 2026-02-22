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
        IStandardExecutor executor,
        int count,
        SandBoxConfiguration? configuration = null,
        IEnumerable<string>? incrementalProperties = null)
    {
        configuration ??= _configuration;
        var propertiesToSweep = incrementalProperties?.ToList() ?? new List<string>();

        var batchRunId = Guid.NewGuid();
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

        await _batchResultFileManager.AppendObjectAsync(batchRunId, batchRunInfo);

        // ── Standard parallel runs ─────────────────────────────────────────────
        await Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            options,
            async (_, _) =>
            {
                var result = await executor.RunAndCaptureAsync(configuration);

                await _batchResultFileManager.AppendObjectAsync(batchRunId, result);

                Interlocked.Increment(ref completedRuns);
                Interlocked.Add(ref totalTurns, result.TurnsCount);
                if (result.WinReason.HasValue)
                    Interlocked.Increment(ref wins);
            });

        // ── Incremental sweep runs ─────────────────────────────────────────────
        bool areaEnabled = configuration.MapSettings.Size.IncrementalArea?.IsEnabled == true;

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
            for (int i = 0; i < stepCount; i++)
            {
                int currentValue = range.Min + i * range.Step;
                var overriddenConfig = WithPropertyOverride(configuration, propertyName, currentValue);

                var result = await executor.RunAndCaptureAsync(overriddenConfig);
                await _batchResultFileManager.AppendObjectAsync(batchRunId, result);

                Interlocked.Increment(ref completedRuns);
                Interlocked.Add(ref totalTurns, result.TurnsCount);
                if (result.WinReason.HasValue)
                    Interlocked.Increment(ref wins);
            }
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

            for (int i = 0; i < iterations; i++)
            {
                int wValue = Math.Min(sz.Width.Min  + i * areaStep, sz.Width.Max);
                int hValue = Math.Min(sz.Height.Min + i * areaStep, sz.Height.Max);

                var overriddenConfig = WithAreaOverride(configuration, wValue, hValue);

                var result = await executor.RunAndCaptureAsync(overriddenConfig);
                await _batchResultFileManager.AppendObjectAsync(batchRunId, result);

                Interlocked.Increment(ref completedRuns);
                Interlocked.Add(ref totalTurns, result.TurnsCount);
                if (result.WinReason.HasValue)
                    Interlocked.Increment(ref wins);
            }
        }

        // ── Summary ────────────────────────────────────────────────────────────
        int losses = completedRuns - wins;
        double avgTurns = completedRuns > 0 ? (double)totalTurns / completedRuns : 0;
        await _batchResultFileManager.AppendObjectAsync(batchRunId, new BatchSummary(completedRuns, wins, losses, avgTurns));
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
