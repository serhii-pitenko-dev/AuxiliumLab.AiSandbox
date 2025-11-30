namespace AiSandBox.Domain.State;

public class PlaygroundHistoryData
{
    public Guid Id { get; init; }
    public List<PlaygroundState> States { get; set; } = new();
}
