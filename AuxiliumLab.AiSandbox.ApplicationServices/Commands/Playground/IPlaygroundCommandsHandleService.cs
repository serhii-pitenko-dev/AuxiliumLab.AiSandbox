using AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground.CreatePlayground;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground;

public interface IPlaygroundCommandsHandleService
{
    public ICreatePlaygroundCommandHandler CreatePlaygroundCommand { get; }
}