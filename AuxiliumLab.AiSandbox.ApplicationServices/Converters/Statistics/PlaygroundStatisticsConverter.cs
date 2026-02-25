using AuxiliumLab.AiSandbox.Domain.Playgrounds;
using AuxiliumLab.AiSandbox.Domain.Statistics.Entities;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Converters.Statistics;

public class PlaygroundStatisticsConverter
{
    public PlayGroundStatistics ConvertToAgentStatistics(StandardPlayground playground, PlayGroundStatistics previous)
    {
        var playgroundStatistics = new PlayGroundStatistics();

        if (playground.Turn == 1 || previous == null)
        {
            var result = InitializeStatistics(playground);

            var heroIndex = Array.FindIndex(result.AgentsStatistics, 
                e => e.CellType == SharedBaseTypes.ValueObjects.ObjectType.Hero);

            var hero = result.AgentsStatistics[heroIndex];
            result.AgentsStatistics[heroIndex] = hero with 
            { 
                Path = [new AgentPath(playground.Turn, [playground.Hero.Coordinates])] 
            };

            // Update path for all enemies with their initial coordinates
            foreach (var enemy in playground.Enemies)
            {
                var enemyIndex = Array.FindIndex(result.AgentsStatistics, e => e.id == enemy.Id);
                if (enemyIndex >= 0)
                {
                    var enemyStats = result.AgentsStatistics[enemyIndex];
                    result.AgentsStatistics[enemyIndex] = enemyStats with 
                    { 
                        Path = [new AgentPath(playground.Turn, [enemy.Coordinates])] 
                    };
                }
            }

            return result;
        }

        // Create a dictionary for fast lookup of previous statistics by agent ID
        var previousStatsDict = previous.AgentsStatistics.ToDictionary(a => a.id);
        var updatedStatisticsList = new List<AgentStatistics>(previousStatsDict.Count);

        // Update Hero statistics
        if (playground.Hero != null && previousStatsDict.TryGetValue(playground.Hero.Id, out var heroPrevious))
        {
            var updatedPath = heroPrevious.Path
                .Concat([new AgentPath(playground.Turn, playground.Hero.PathToTarget.ToArray())])
                .ToArray();
            
            updatedStatisticsList.Add(new AgentStatistics(
                playground.Hero.Id,
                playground.Turn,
                playground.Hero.Type,
                updatedPath
            ));
        }

        // Update Enemy statistics
        foreach (var enemy in playground.Enemies)
        {
            if (previousStatsDict.TryGetValue(enemy.Id, out var enemyPrevious))
            {
                var updatedPath = enemyPrevious.Path
                    .Concat([new AgentPath(playground.Turn, enemy.PathToTarget.ToArray())])
                    .ToArray();
                
                updatedStatisticsList.Add(new AgentStatistics(
                    enemy.Id,
                    playground.Turn,
                    enemy.Type,
                    updatedPath
                ));
            }
        }

        playgroundStatistics.AgentsStatistics = updatedStatisticsList.ToArray();
        
        return playgroundStatistics;
    }

    private PlayGroundStatistics InitializeStatistics(StandardPlayground playground)
    {
        var agentStatisticsList = new List<AgentStatistics>();

        // Convert Hero if it exists
        if (playground.Hero != null)
        {
            agentStatisticsList.Add(new AgentStatistics(
                playground.Hero.Id,
                playground.Turn,
                playground.Hero.Type,
                [new AgentPath(playground.Turn, playground.Hero.PathToTarget.ToArray())]
            ));
        }

        // Convert all Enemies
        foreach (var enemy in playground.Enemies)
        {
            agentStatisticsList.Add(new AgentStatistics(
                enemy.Id,
                playground.Turn,
                enemy.Type,
                [new AgentPath(playground.Turn, enemy.PathToTarget.ToArray())]
            ));
        }

        var playgroundStatistics = new PlayGroundStatistics
        {
            AgentsStatistics = agentStatisticsList.ToArray()
        };

        return playgroundStatistics;
    }
}