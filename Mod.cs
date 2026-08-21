using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Cities2PedestrianTraffic.Systems;

namespace Cities2PedestrianTraffic;

public sealed class Mod : IMod
{
    public static readonly ILog Log = LogManager
        .GetLogger("Cities2PedestrianTraffic")
        .SetShowsErrorsInUI(false);

    public void OnLoad(UpdateSystem updateSystem)
    {
        Log.Info("Loading Cities2PedestrianTraffic 0.1.0");

        if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
        {
            Log.Info($"Mod asset: {asset.path}");
        }

        // Throw away group ids derived from an old road layout before vanilla rebuilds the node.
        updateSystem.UpdateBefore<IntersectionSignalResetSystem, Game.Net.TrafficLightInitializationSystem>(SystemUpdatePhase.Modification4B);

        // The vanilla initialization stays authoritative. We only amend its lane groups afterwards.
        updateSystem.UpdateAfter<IntersectionSignalSetupSystem, Game.Net.TrafficLightInitializationSystem>(SystemUpdatePhase.Modification4B);

        // Hold a pedestrian-only phase by feeding demand into the vanilla state machine.
        updateSystem.UpdateBefore<PedestrianPhaseHoldSystem, Game.Simulation.TrafficLightSystem>(SystemUpdatePhase.GameSimulation);

        // Added right-turn groups are Yield, never unconditional Go.
        updateSystem.UpdateAfter<FreeRightTurnYieldSystem, Game.Simulation.TrafficLightSystem>(SystemUpdatePhase.GameSimulation);

        updateSystem.UpdateAt<TrafficSelectionToolSystem>(SystemUpdatePhase.ToolUpdate);
        updateSystem.UpdateAt<TrafficUISystem>(SystemUpdatePhase.UIUpdate);
    }

    public void OnDispose()
    {
        Log.Info("Disposing Cities2PedestrianTraffic");
    }
}
