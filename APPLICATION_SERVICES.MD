# Execution Logic
- Stored in the folder (AuxiliumLab.AiSandbox.ApplicationServices/Runner), Executor classes.
- Use [commands](AuxiliumLab.AiSandbox.ApplicationServices/Commands) for creating Playground 
- Communication with Ai - trough IMessageBroker and IBrokerRpcClient. IBrokerRpcClient is used to waite response, IMessageBroker is "fire and forget".
- Data is getting directly from memory or file system using AuxiliumLab.AiSandbox.Infrastructure.

## Main steps
1. Creating playground using command and save it to memory
2. Load Playground from memory
4. Subscribe on Ai Ready message, it will trigger start first turn
5. Do preporation game actions and send message that simulation is started
6. OnAiReadyToActionsEvent - start simulation
7. Simulation is finished when turn limit is reached or hero reach exit cell or hero is catched by enemy

### General simulation flow
1. Get agents to act. Playground returns ordered list of agents that can act this turn. 
2. In the endless loop get next agent to act until all agents acted.

### Agent simulation flow
1. Get agent to act
2. Send action request to AI module for the agent and wait for response.
3. Create data for AI module acording to map and agent state.