using AiSandBox.Ai.Configuration;
using AiSandBox.Startup.Configuration;

namespace AiSandBox.Startup.Menu;

internal interface IMenuRunner
{
    /// <summary>
    /// Interactively resolves startup settings from user input.
    /// </summary>
    /// <param name="defaults">Default settings from appsettings.json.</param>
    /// <returns>Resolved settings and the selected algorithm (null unless Training was chosen).</returns>
    (StartupSettings Settings, ModelType? SelectedAlgorithm) ResolveSettings(StartupSettings defaults);
}
