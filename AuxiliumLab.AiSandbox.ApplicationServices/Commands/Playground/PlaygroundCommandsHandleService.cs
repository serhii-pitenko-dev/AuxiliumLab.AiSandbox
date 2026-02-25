using AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground.CreatePlayground;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground;

public class PlaygroundCommandsHandleService(
    ICreatePlaygroundCommandHandler createMapCommandHandler
    ) : IPlaygroundCommandsHandleService
{
    public ICreatePlaygroundCommandHandler CreatePlaygroundCommand { get; } = createMapCommandHandler;
}

