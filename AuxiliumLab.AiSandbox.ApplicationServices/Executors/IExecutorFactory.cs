namespace AuxiliumLab.AiSandbox.ApplicationServices.Executors;

public interface IExecutorFactory
{
    IExecutorForPresentation CreateExecutorForPresentation();
    IStandardExecutor CreateStandardExecutor();
}