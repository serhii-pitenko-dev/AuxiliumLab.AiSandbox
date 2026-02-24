namespace AiSandBox.ApplicationServices.Executors;

public interface IExecutorFactory
{
    IExecutorForPresentation CreateExecutorForPresentation();
    IStandardExecutor CreateStandardExecutor();
}