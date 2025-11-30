namespace AiSandBox.Infrastructure.Configuration.Preconditions;

public class SandBoxConfiguration
{
    public MapConfiguration MapSettings { get; set; }
    public HeroConfiguration Hero { get; set; }
    public EnemyConfiguration Enemy { get; set; }
    public int TurnTimeout { get; set; }
    public int MaxTurns { get; set; }
    public int SaveToFileRegularity { get; set; }
}
