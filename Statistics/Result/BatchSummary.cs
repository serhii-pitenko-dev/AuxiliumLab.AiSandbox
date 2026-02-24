namespace AiSandBox.Domain.Statistics.Result;

/// <summary>
/// Contains aggregated summary information for an entire batch run.
/// Saved once after all runs in the batch have completed.
/// </summary>
public record BatchSummary(
    Guid Id,
    int TotalRuns,
    int Wins,
    int Losses,
    double AverageTurns);
