namespace AiSandBox.Domain.Agents.Entities;

public struct InitialAgentCharacters 
{
    public int Speed { get; private init; }
    public int SightRange { get; private init; }
    public int Stamina { get; private init; }

    public InitialAgentCharacters(int speed, int sightRange, int stamina)
    {
        Speed = speed;
        SightRange = sightRange;
        Stamina = stamina;
    }
}

