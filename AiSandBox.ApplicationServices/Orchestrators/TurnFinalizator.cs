using AiSandBox.ApplicationServices.Runner;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Orchestrators;

public class TurnFinalizator: ITurnFinalizator
{
    private readonly IMemoryDataManager<StandardPlayground> _sandboxRepository;
    private readonly IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository;
    private readonly IFileDataManager<PlayGroundStatistics> statisticsFileRepository;
    private readonly SandBoxConfiguration _configuration;
    public event Action<Guid>? TurnFinalized;

    public TurnFinalizator(
        IExecutor executor,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IOptions<SandBoxConfiguration> configuration)
    {
    }

    private void Orchestrate(Guid playgroundId)
    {
        
    }

    protected virtual void OnTurnFinalized(Guid playgroundId)
    {
        TurnFinalized?.Invoke(playgroundId);
    }
}

