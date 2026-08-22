using Cities2PedestrianTraffic.Components;
using Game;
using Game.Common;
using Game.Net;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace Cities2PedestrianTraffic.Systems
{

/// <summary>
/// Keeps the vanilla state machine on the dedicated pedestrian group for a minimum interval
/// by supplying pedestrian demand before TrafficLightSystem evaluates the next group.
/// It never writes TrafficLights.m_State/m_Timer directly.
/// </summary>
public partial class PedestrianPhaseHoldSystem : GameSystemBase
{
    // CS2 simulation uses roughly 60 simulation frames per second at 1x.
    // No user configuration: v0.1 deliberately uses a fixed ~10 second minimum phase.
    public const uint MinimumPedestrianFrames = 600;

    private EntityQuery m_Query;
    private SimulationSystem m_SimulationSystem = null!;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        m_Query = GetEntityQuery(
            ComponentType.ReadOnly<IntersectionTrafficConfig>(),
            ComponentType.ReadWrite<IntersectionTrafficRuntime>(),
            ComponentType.ReadOnly<TrafficLights>(),
            ComponentType.ReadOnly<SubLane>(),
            ComponentType.Exclude<Deleted>(),
            ComponentType.Exclude<Destroyed>(),
            ComponentType.Exclude<Temp>());
        RequireForUpdate(m_Query);
    }

    protected override void OnUpdate()
    {
        uint frame = m_SimulationSystem.frameIndex;
        using NativeArray<Entity> intersections = m_Query.ToEntityArray(Allocator.Temp);

        foreach (Entity intersection in intersections)
        {
            IntersectionTrafficConfig config = EntityManager.GetComponentData<IntersectionTrafficConfig>(intersection);
            IntersectionTrafficRuntime runtime = EntityManager.GetComponentData<IntersectionTrafficRuntime>(intersection);
            TrafficLights trafficLights = EntityManager.GetComponentData<TrafficLights>(intersection);

            if (!config.ExclusivePedestrianPhase || runtime.PedestrianGroupMask == 0)
            {
                ResetHold(intersection, runtime);
                continue;
            }

            ushort currentGroup = trafficLights.m_CurrentSignalGroup > 0
                ? (ushort)(1u << (trafficLights.m_CurrentSignalGroup - 1))
                : (ushort)0;

            bool pedestrianPhase =
                (runtime.PedestrianGroupMask & currentGroup) != 0 &&
                trafficLights.m_State == TrafficLightState.Ongoing;

            if (!pedestrianPhase)
            {
                ResetHold(intersection, runtime);
                continue;
            }

            if (!runtime.PedestrianHoldActive)
            {
                runtime.PedestrianHoldActive = true;
                runtime.PedestrianHoldStartFrame = frame;
                EntityManager.SetComponentData(intersection, runtime);
            }

            uint elapsed = frame - runtime.PedestrianHoldStartFrame;
            if (elapsed >= MinimumPedestrianFrames)
            {
                continue;
            }

            DynamicBuffer<SubLane> subLanes = EntityManager.GetBuffer<SubLane>(intersection, true);
            for (int i = 0; i < subLanes.Length; i++)
            {
                Entity lane = subLanes[i].m_SubLane;
                if (!EntityManager.HasComponent<PedestrianLane>(lane) || !EntityManager.HasComponent<LaneSignal>(lane))
                {
                    continue;
                }

                LaneSignal signal = EntityManager.GetComponentData<LaneSignal>(lane);
                if ((signal.m_GroupMask & runtime.PedestrianGroupMask) == 0)
                {
                    continue;
                }

                signal.m_Priority = 127;
                signal.m_Petitioner = lane;
                EntityManager.SetComponentData(lane, signal);
            }
        }
    }

    private void ResetHold(Entity intersection, IntersectionTrafficRuntime runtime)
    {
        if (!runtime.PedestrianHoldActive && runtime.PedestrianHoldStartFrame == 0)
        {
            return;
        }

        runtime.PedestrianHoldActive = false;
        runtime.PedestrianHoldStartFrame = 0;
        EntityManager.SetComponentData(intersection, runtime);
    }
}
}
