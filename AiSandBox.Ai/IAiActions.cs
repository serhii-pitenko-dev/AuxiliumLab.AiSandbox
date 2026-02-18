using AiSandBox.Ai.Configuration;

namespace AiSandBox.Ai;

public interface IAiActions
{
    AiConfiguration AiConfiguration { get; init; }
    void Initialize();
}

