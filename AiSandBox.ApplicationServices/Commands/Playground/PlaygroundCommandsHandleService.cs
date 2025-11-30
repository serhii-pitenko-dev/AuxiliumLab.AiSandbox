using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.ApplicationServices.Commands.Playground.InitializePlaygroundFromFile;

namespace AiSandBox.ApplicationServices.Commands.Playground;

public class PlaygroundCommandsHandleService(
    ICreatePlaygroundCommandHandler createMapCommandHandler,
    IInitializePlaygroundFromFileCommandHandler initializeMapFromFileCommandHandler
    ) : IPlaygroundCommandsHandleService
{
    public ICreatePlaygroundCommandHandler CreatePlaygroundCommand { get; } = createMapCommandHandler;

    public IInitializePlaygroundFromFileCommandHandler InitializePlaygroundFromFileCommand { get; } = initializeMapFromFileCommandHandler;
}

