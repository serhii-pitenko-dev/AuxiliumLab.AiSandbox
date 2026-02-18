namespace AiSandBox.SharedBaseTypes.ValueObjects.StartupSettings;

public enum PresentationMode
{
    /// <summary>
    /// No visualization — used for training and mass run modes.
    /// </summary>
    WithoutVisualization = 0,
    /// <summary>
    /// Console presentation mode.
    /// </summary>
    Console,
    /// <summary>
    /// Web presentation mode.
    /// </summary>
    Web
}