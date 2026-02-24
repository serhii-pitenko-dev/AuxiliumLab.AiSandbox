namespace AiSandBox.Domain.Statistics.Result;

/// <summary>
/// Contains aggregated summary information for a single execution phase within a mass run.
/// One record is saved per phase (standard runs, each property sweep, area sweep).
/// </summary>
public record BatchSummary(
    Guid Id,
    int TotalRuns,
    int Wins,
    int Losses,
    double AverageTurns,
    string Description);
