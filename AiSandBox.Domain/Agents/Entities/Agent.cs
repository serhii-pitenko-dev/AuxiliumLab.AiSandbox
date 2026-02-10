using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Validation.Agents;
using AiSandBox.SharedBaseTypes.ValueObjects;
using System.Text.Json.Serialization;

namespace AiSandBox.Domain.Agents.Entities;

public abstract class Agent: SandboxMapBaseObject
{
    private AgentActionAddValidator _agentActionValidator = new AgentActionAddValidator();

    public List<AgentAction> AvailableActions { get; private set; } = new();

    public List<AgentAction> ExecutedActions { get; private set; } = new();

    // Parameterless constructor for deserialization
    protected Agent() : base()
    {
    }

    public List<Cell> VisibleCells { get; protected set; } = new();
    public Agent(
        ObjectType cellType,
        InitialAgentCharacters characters,
        Cell? cell, 
        Guid id) : base(cellType, cell, id)
    {
        Speed = characters.Speed;
        SightRange = characters.SightRange;
        MaxStamina = Stamina = characters.Stamina;
        PathToTarget = characters.PathToTarget;
        AvailableActions = characters.AgentActions;
        ExecutedActions = characters.ExecutedActions;
        IsRun = characters.isRun;
        OrderInTurnQueue = characters.orderInTurnQueue;
    }

    public List<Coordinates> PathToTarget { get; protected set; } = [];
    public int Speed { get; private set; }
    public int SightRange { get; protected set; }
    public bool IsRun { get; protected set; } = false;
    public int Stamina { get; protected set; }
    public int MaxStamina { get; protected set; }
    public int OrderInTurnQueue { get; set; } = 0;
    protected void CopyBaseTo(Agent target)
    {
        base.CopyTo(target);
        target.Speed = Speed;
        target.SightRange = SightRange;
        target.IsRun = IsRun;
        target.Stamina = Stamina;
        target.MaxStamina = MaxStamina;
        target.PathToTarget = [.. PathToTarget];
        target.VisibleCells = [.. VisibleCells];
        target.Transparent = Transparent;
    }
    public void ResetPath()
    {
        PathToTarget.Clear();
    }

    /// <summary>
    /// Performs the specified action for the agent.
    /// <remarks>Agent cannot move itself, so only non-movement actions are handled here.</remarks>
    /// </summary>
    public virtual bool DoAction(AgentAction action, bool isActivated)
    {
        switch (action)
        {
            case AgentAction.Run:
                UpdateActionsListOnExecute(AgentAction.Run);
                if (!isActivated)
                {
                    StopRunning();
                    break;
                }
                if (IsRun)
                    break;
                Run();
                break;
            case AgentAction.Move:
                throw new InvalidOperationException("Agent cannot move itself. Movement should be handled externally.");
        }

        return true;
    }

    public virtual void GetReadyForNewTurn()
    {
        ResetPath();
        ReCalculateAvailableActions();
        RestoringCharacteristics();
        //add current/started position to path
        PathToTarget = new List<Coordinates>() { Coordinates };
    }

    private void RestoringCharacteristics()
    {
        int restoredStamina = Stamina + (MaxStamina / 3);
        Stamina = restoredStamina > MaxStamina ? MaxStamina : restoredStamina;
    }

    public void SetOrderInTurnQueue(int order)
    {
        OrderInTurnQueue = order;
    }

    /// <summary>
    /// Calculates and updates the set of available limited actions for the agent based on its current state.
    /// </summary>
    /// <remarks>At the beginning this method clears the existing limited actions and determines new ones based on the agent state </remarks>
    public void ReCalculateAvailableActions()
    {
        AvailableActions.Clear();
        ExecutedActions.Clear();    

        AddNewActions(new List<AgentAction> { AgentAction.Run });

        int possibleMoves = Stamina >= Speed ? Speed : Stamina;
        AddNewActions(Enumerable.Repeat(AgentAction.Move, possibleMoves).ToList());
        if (IsRun)
            Run();
    }

    public void AddNewActions(List<AgentAction> action)
    {
        _agentActionValidator.Validate(this, AvailableActions, action);
        AvailableActions.AddRange(action);
    }

    /// <summary>
    /// Activate run ability on Agent
    /// Increases available movements by double, depending on Stamina. 
    /// If stamina is less than required for full run, adds as much as stamina allows.
    /// </summary>
    protected void Run()
    {
        IsRun = true;
        int avaliableBeforeRunMovements = AvailableActions.Where(a => a == AgentAction.Move).Count();
        int afterRunMovements = avaliableBeforeRunMovements * 2;
        int toAdd = (afterRunMovements > Stamina ? Stamina : afterRunMovements) - avaliableBeforeRunMovements;
        AddNewActions(Enumerable.Repeat(AgentAction.Move, toAdd).ToList());
    }

    /// <summary>
    /// Deactivate run ability on Agent
    /// Decreases available movements by half.
    /// </summary>
    protected void StopRunning()
    {
        if (!IsRun)
            return;
        IsRun = false;
        int runMovesToRemove = AvailableActions.Where(a => a == AgentAction.Move).Count() / 2;
        for (int i = 0; i < runMovesToRemove; i++)
        {
            AvailableActions.Remove(AgentAction.Move);
        }
    }

    /// <summary>
    /// If agent moved, update its path and executed actions
    /// </summary>
    public void AgentWasMoved(Coordinates coordinates)
    {
        Stamina--;
        PathToTarget.Add(coordinates);

        UpdateActionsListOnExecute(AgentAction.Move);
    }

    public void ActionFailed(AgentAction action)
    {
        AvailableActions.Remove(action);
        ExecutedActions.Add(action);
    }

    protected void UpdateActionsListOnExecute(AgentAction action)
    {
        AvailableActions.Remove(action);
        ExecutedActions.Add(action);
    }
}