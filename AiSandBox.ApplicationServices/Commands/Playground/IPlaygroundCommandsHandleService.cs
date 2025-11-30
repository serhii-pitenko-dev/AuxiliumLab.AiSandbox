using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.ApplicationServices.Commands.Playground.InitializePlaygroundFromFile;

namespace AiSandBox.ApplicationServices.Commands.Playground;

public interface IPlaygroundCommandsHandleService
{
    public ICreatePlaygroundCommandHandler CreatePlaygroundCommand { get; }

    public IInitializePlaygroundFromFileCommandHandler InitializePlaygroundFromFileCommand { get; }
}