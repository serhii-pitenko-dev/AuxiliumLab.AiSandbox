using AiSandBox.ApplicationServices.Runner;

namespace AiSandBox.Startup.Runners;

public class RunSimulations
{
    public async Task RunSingleAsync(IExecutorForPresentation executor)
        => await executor.RunAsync();

    public async Task RunSingleTrainedAsync(IStandardExecutor executor)
        => await executor.RunAsync();

    public async Task RunTestPreconditionsAsync(IExecutorForPresentation executor)
        => await executor.TestRunWithPreconditionsAsync();

    public async Task RunManyAsync(IExecutorForPresentation executor, int count)
    {
        for (int i = 0; i < count; i++)
            await executor.RunAsync();
    }
}


