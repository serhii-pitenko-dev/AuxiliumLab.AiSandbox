using AiSandBox.Ai;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Runner.LogsDto;
using AiSandBox.ApplicationServices.Runner.LogsDto.Performance;
using AiSandBox.ApplicationServices.Runner.TestPreconditionSet;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSandBox.ApplicationServices.Executors;

public class ExecutorFactory : IExecutorFactory
{
    private readonly IPlaygroundCommandsHandleService _mapCommands;
    private readonly IMemoryDataManager<StandardPlayground> _sandboxRepository;
    private readonly IAiActions _aiActions;
    private readonly IOptions<SandBoxConfiguration> _configuration;
    private readonly IMemoryDataManager<PlayGroundStatistics> _statisticsMemoryRepository;
    private readonly IFileDataManager<PlayGroundStatistics> _statisticsFileRepository;
    private readonly IFileDataManager<StandardPlaygroundState> _playgroundStateFileRepository;
    private readonly IMemoryDataManager<AgentStateForAIDecision> _agentStateMemoryRepository;
    private readonly IMessageBroker _messageBroker;
    private readonly IBrokerRpcClient _brokerRpcClient;
    private readonly IStandardPlaygroundMapper _standardPlaygroundMapper;
    private readonly IFileDataManager<RawDataLog> _rawDataLogFileRepository;
    private readonly IFileDataManager<TurnExecutionPerformance> _turnExecutionPerformanceFileRepository;
    private readonly IFileDataManager<SandboxExecutionPerformance> _sandboxExecutionPerformanceFileRepository;
    private readonly ITestPreconditionData _testPreconditionData;

    public ExecutorFactory(IPlaygroundCommandsHandleService mapCommands,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IAiActions aiActions,
        IOptions<SandBoxConfiguration> configuration,
        IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository,
        IFileDataManager<PlayGroundStatistics> statisticsFileRepository,
        IFileDataManager<StandardPlaygroundState> playgroundStateFileRepository,
        IMemoryDataManager<AgentStateForAIDecision> agentStateMemoryRepository,
        IMessageBroker messageBroker,
        IBrokerRpcClient brokerRpcClient,
        IStandardPlaygroundMapper standardPlaygroundMapper,
        IFileDataManager<RawDataLog> rawDataLogFileRepository,
        IFileDataManager<TurnExecutionPerformance> turnExecutionPerformanceFileRepository,
        IFileDataManager<SandboxExecutionPerformance> sandboxExecutionPerformanceFileRepository,
        ITestPreconditionData testPreconditionData)
    {
        _mapCommands = mapCommands;
        _sandboxRepository = sandboxRepository;
        _aiActions = aiActions;
        _configuration = configuration;
        _statisticsMemoryRepository = statisticsMemoryRepository;
        _statisticsFileRepository = statisticsFileRepository;
        _playgroundStateFileRepository = playgroundStateFileRepository;
        _agentStateMemoryRepository = agentStateMemoryRepository;
        _messageBroker = messageBroker;
        _brokerRpcClient = brokerRpcClient;
        _standardPlaygroundMapper = standardPlaygroundMapper;
        _rawDataLogFileRepository = rawDataLogFileRepository;
        _turnExecutionPerformanceFileRepository = turnExecutionPerformanceFileRepository;
        _sandboxExecutionPerformanceFileRepository = sandboxExecutionPerformanceFileRepository;
        _testPreconditionData = testPreconditionData;
    }

    public IExecutorForPresentation CreateExecutorForPresentation()
    {
        return new ExecutorForPresentation(
            _mapCommands,
            _sandboxRepository,
            _aiActions,
            _configuration,
            _statisticsMemoryRepository,
            _statisticsFileRepository,
            _playgroundStateFileRepository,
            _agentStateMemoryRepository,
            _messageBroker,
            _brokerRpcClient,
            _standardPlaygroundMapper,
            _rawDataLogFileRepository,
            _turnExecutionPerformanceFileRepository,
            _sandboxExecutionPerformanceFileRepository,
            _testPreconditionData);
    }

    public IStandardExecutor CreateStandardExecutor()
    {
        return new StandardExecutor(
            _mapCommands,
            _sandboxRepository,
            _aiActions,
            _configuration,
            _statisticsMemoryRepository,
            _statisticsFileRepository,
            _playgroundStateFileRepository,
            _agentStateMemoryRepository,
            _messageBroker,
            _brokerRpcClient,
            _standardPlaygroundMapper,
            _rawDataLogFileRepository,
            _turnExecutionPerformanceFileRepository,
            _sandboxExecutionPerformanceFileRepository,
            _testPreconditionData);
    }
}