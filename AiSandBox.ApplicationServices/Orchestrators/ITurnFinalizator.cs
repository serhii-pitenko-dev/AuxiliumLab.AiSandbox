namespace AiSandBox.ApplicationServices.Orchestrators;

public interface ITurnFinalizator
{
    public event Action<Guid>? TurnFinalized;
}

