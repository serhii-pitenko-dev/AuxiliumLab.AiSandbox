namespace AiSandBox.ConsolePresentation.Settings;

public class ConsoleSettings
{
    public ConsoleSize ConsoleSize { get; set; } = new();
    public ColorScheme ColorScheme { get; set; } = new();
    public int MovementTimeout { get; set; }
}