using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSandBox.SharedBaseTypes.ValueObjects.StartupSettings;

public enum ExecutionMode
{
    /// <summary>
    /// Trains the AI using the Python RL Training Service. This mode is used for training new policies and requires a connection to the training service.
    /// </summary>
    Training = 0,
    /// <summary>
    /// Runs a single random agent actions simulation.
    /// </summary>
    SingleRandomAISimulation,
    /// <summary>
    /// Runs a single AI simulation.
    /// </summary>
    SingleTrainedAISimulation,
    /// <summary>
    /// Runs multiple random AI simulations simultaneously to gather statistical data.
    /// </summary>  
    MassRandomAISimulation,
    /// <summary>
    /// Runs multiple trained AI simulations simultaneously to gather statistical data.
    /// </summary>  
    MassTrainedAISimulation,
    /// <summary>
    /// Load a previously recorded simulation from a file and play it back. This mode is used for analyzing past simulations and does not require a connection to the training service.
    /// </summary>
    LoadSimulation,
    /// <summary>
    /// Generate playground with preconditions and test them. This mode is used for testing specific scenarios and does not require a connection to the training service.
    /// </summary>
    TestPreconditions
}