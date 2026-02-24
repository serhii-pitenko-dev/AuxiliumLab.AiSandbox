using AiSandBox.Domain.Statistics.Result;
using AiSandBox.Statistics.Preconditions;
using System.Text;

namespace AiSandBox.Statistics.Converters;

/// <summary>
/// Converts simulation precondition and result objects to CSV-formatted strings
/// suitable for appending to a statistics file (e.g. for import into Google Sheets).
/// </summary>
public static class TableConverter
{
    // ── Public conversion methods ──────────────────────────────────────────────

    /// <summary>
    /// Converts <see cref="SimulationStartupSettings"/> to a CSV table where
    /// each row is a property name and the single column contains its value.
    /// </summary>
    public static string ToCsv(SimulationStartupSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Startup Settings");
        sb.AppendLine("Property,Value");
        sb.AppendLine($"PolicyType,{Escape(settings.PolicyType)}");
        sb.AppendLine($"ExecutionMode,{Escape(settings.ExecutionMode)}");
        sb.AppendLine($"SimulationCount,{settings.SimulationCount}");
        sb.AppendLine($"IncrementalProperties,{Escape(string.Join(";", settings.IncrementalProperties))}");
        return sb.ToString();
    }

    /// <summary>
    /// Converts <see cref="SimulationSandBoxSettings"/> to a CSV table where
    /// rows are property names and columns are Min, Current, Max, Step.
    /// Boolean / scalar properties have only the Current column populated.
    /// </summary>
    public static string ToCsv(SimulationSandBoxSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SandBox Settings");
        sb.AppendLine("Property,Min,Current,Max,Step");

        AppendRange(sb, "MaxTurns",          settings.MaxTurns);
        AppendRange(sb, "MapWidth",          settings.MapWidth);
        AppendRange(sb, "MapHeight",         settings.MapHeight);
        AppendRange(sb, "BlocksPercent",     settings.BlocksPercent);
        AppendRange(sb, "PercentOfEnemies",  settings.PercentOfEnemies);
        AppendRange(sb, "HeroSpeed",         settings.HeroSpeed);
        AppendRange(sb, "HeroSightRange",    settings.HeroSightRange);
        AppendRange(sb, "HeroStamina",       settings.HeroStamina);
        AppendRange(sb, "EnemySpeed",        settings.EnemySpeed);
        AppendRange(sb, "EnemySightRange",   settings.EnemySightRange);
        AppendRange(sb, "EnemyStamina",      settings.EnemyStamina);
        AppendScalar(sb, "IncrementalAreaEnabled", settings.IncrementalAreaEnabled.ToString());
        AppendScalar(sb, "IncrementalAreaStep",    settings.IncrementalAreaStep.ToString());

        return sb.ToString();
    }

    /// <summary>
    /// Converts a list of <see cref="BatchSummary"/> to a CSV table where
    /// rows are property names (Id, TotalRuns, Wins, Losses, AverageTurns)
    /// and each column corresponds to one <see cref="BatchSummary"/> identified by its <see cref="BatchSummary.Id"/>.
    /// </summary>
    public static string ToCsv(IList<BatchSummary> batches)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Batch Summary");

        if (batches.Count == 0)
        {
            sb.AppendLine("(no batch data)");
            return sb.ToString();
        }

        // Header row: "Property", then each batch Id
        var headerParts = new List<string> { "Property" };
        headerParts.AddRange(batches.Select(b => Escape(b.Id.ToString())));
        sb.AppendLine(string.Join(",", headerParts));

        // Data rows (transposed)
        AppendTransposedRow(sb, "Id",           batches, b => Escape(b.Id.ToString()));
        AppendTransposedRow(sb, "TotalRuns",    batches, b => b.TotalRuns.ToString());
        AppendTransposedRow(sb, "Wins",         batches, b => b.Wins.ToString());
        AppendTransposedRow(sb, "Losses",       batches, b => b.Losses.ToString());
        AppendTransposedRow(sb, "AverageTurns", batches, b => b.AverageTurns.ToString("F2"));

        return sb.ToString();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static void AppendRange(StringBuilder sb, string name, RangeSettings range)
        => sb.AppendLine($"{Escape(name)},{range.Min},{range.Current},{range.Max},{range.Step}");

    private static void AppendScalar(StringBuilder sb, string name, string value)
        => sb.AppendLine($"{Escape(name)},,{Escape(value)},,");

    private static void AppendTransposedRow(
        StringBuilder sb,
        string rowLabel,
        IList<BatchSummary> batches,
        Func<BatchSummary, string> selector)
    {
        var parts = new List<string> { Escape(rowLabel) };
        parts.AddRange(batches.Select(selector));
        sb.AppendLine(string.Join(",", parts));
    }

    /// <summary>
    /// Escapes a CSV field: wraps in double-quotes if the value contains
    /// a comma, double-quote, or newline; doubles any embedded double-quotes.
    /// </summary>
    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
