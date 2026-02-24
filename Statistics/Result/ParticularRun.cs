using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;

namespace AiSandBox.Domain.Statistics.Result;

/// <summary>
/// Contains information about the result of a single sandbox run within a batch.
/// Saved after each run completes (on HeroWonEvent or HeroLostEvent).
/// </summary>
public record ParticularRun(
    Guid PlaygroundId,
    int TurnsCount,
    int EnemiesCount,
    WinReason? WinReason,
    LostReason? LostReason);
