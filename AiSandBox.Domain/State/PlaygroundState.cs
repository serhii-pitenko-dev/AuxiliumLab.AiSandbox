using AiSandBox.Domain.Agents.Entities;

namespace AiSandBox.Domain.State;

public class PlaygroundState
{
    public int Turn { get; private init; }

    public Guid Id { get; private init; }

    public Hero Hero { get; private init; }

    public List<Enemy> Enemies { get; private init; }

    public PlaygroundState(int turn, Guid id, Hero hero, List<Enemy> enemies)
    {
        Turn = turn;
        Id = id;
        Hero = hero.Clone();
        Enemies = enemies.Select(e => e.Clone()).ToList();
    }
}

