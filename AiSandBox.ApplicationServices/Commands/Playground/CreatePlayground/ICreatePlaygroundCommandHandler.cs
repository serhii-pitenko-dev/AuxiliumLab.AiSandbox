namespace AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;

public interface ICreatePlaygroundCommandHandler
{
    public Guid Handle(CreatePlaygroundCommandParameters createMapCommandParameters);
}

