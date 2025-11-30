using AiSandBox.Domain.Maps;
using AiSandBox.SharedBaseTypes.ValueObjects;
using System.Text.Json.Serialization;

namespace AiSandBox.Domain.Agents.Entities;

public abstract class Agent: SandboxBaseObject
{
    [JsonInclude]
    public List<Cell> VisibleCells { get; protected set; } = new();

    // Parameterless constructor for deserialization
    protected Agent() : base()
    {
    }

    public Agent(
        ECellType cellType,
        InitialAgentCharacters characters,
        Coordinates coordinates, 
        Guid id) : base(cellType, coordinates, id)
    {
        Speed = characters.Speed;
        SightRange = characters.SightRange;
        MaxStamina = Stamina = characters.Stamina;
    }

    [JsonInclude]
    public List<Coordinates> PathToTarget { get; protected set; } = [];

    [JsonInclude]
    public int Speed { get; protected set; }

    [JsonInclude]
    public int SightRange { get; protected set; }

    [JsonInclude]
    public bool IsRun { get; protected set; } = false;

    [JsonInclude]
    public int Stamina { get; protected set; }

    [JsonInclude]
    public int MaxStamina { get; protected set; }

    protected void CopyBaseTo(Agent target)
    {
        target.Speed = Speed;
        target.SightRange = SightRange;
        target.IsRun = IsRun;
        target.Stamina = Stamina;
        target.MaxStamina = MaxStamina;
        target.PathToTarget = [.. PathToTarget];
        target.Coordinates = Coordinates;
        //target.VisibleCells should be recalculated each turn, so no need to copy
        target.Transparent = Transparent;
    }

    public void ResetPath()
    {
        PathToTarget.Clear();
    }

    public void AddToPath(List<Coordinates> coordinates)
    {
        PathToTarget.AddRange(coordinates);
    }

    public virtual void Move(Coordinates goTo)
    {
        Coordinates = goTo;
        PathToTarget.Add(goTo);
        if (IsRun)
        {
            Stamina = Math.Max(0, Stamina - 1);
            if (Stamina == 0)
            {
                DeActivateAbility([EAbility.Run]);
            }
        }
        else
        {
            if (Stamina < MaxStamina)
            {
                Stamina += 1;
            }
        }
    }

    public virtual void ActivateAbilities(EAbility[] abilities)
    {
        foreach (EAbility ability in abilities)
        {
            switch (ability)
            {
                case EAbility.Run:
                    if (IsRun)
                        break;

                    IsRun = true;
                    Speed += 1;

                    break;
            }
        }
    }

    public virtual void DeActivateAbility(EAbility[] abilities)
    {
        foreach (EAbility ability in abilities)
        {
            switch (ability)
            {
                case EAbility.Run:
                    if (!IsRun)
                        break;

                    IsRun = false;
                    Speed -= 1;
                    break;
            }
        }
    }

    public virtual void GetReadyForNewTurn()
    {
        ResetPath();

        //add current/started position to path
        AddToPath(new List<Coordinates>() { Coordinates });
    }
}