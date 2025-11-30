namespace AiSandBox.ApplicationServices.Commands.Playground.InitializePlaygroundFromFile;

public interface IInitializePlaygroundFromFileCommandHandler
{
    void Handle(InitializePlaygroundFromFileCommandParameters commandParameters);
}