using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Playgrounds;

namespace AiSandBox.Domain.Agents.Services.Vision;

public interface IVisibilityService
{
    void UpdateVisibleCells(Agent agent, StandardPlayground playground);
}