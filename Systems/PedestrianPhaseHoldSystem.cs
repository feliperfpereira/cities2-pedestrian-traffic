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
/// by supplying pedestrian demand immediately before vanilla evaluates the same UpdateFrame slice.
/// </summary>
public partial class PedestrianPhaseHoldSystem : GameSystemBase
{
    public const uint MinimumPedestrianFrames = 600;
    private const int UpdateFrameBuckets = 16;

    private EntityQuery m_Query;
    private SimulationSystem m_SimulationSystem = null!;

    public override int GetUpdateInterval(SystemUpdatePhase phase) => 4;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        m_Query = GetEntityQuery(
            ComponentType.ReadWrite<IntersectionTrafficRuntime>(),
            ComponentType.ReadOnly<TrafficLights>(),
            ComponentType.ReadOnly<SubLane>(),
            ComponentType.ReadOnly<UpdateFrame>(),
            ComponentType.Exclude<Deleted>(),
            ComponentType.Exclude<Destroyed>(),
            ComponentType.Exclude<Temp>());
        RequireForUpdate(m_Query);
    }

    protected override void OnUpdate()
    {
        uint frame = m_SimulationSystem.frameIndex;
        m_Query.ResetFilter();
        m_Query.SetSharedComponentFilter(new UpdateFrame(
            SimulationUtils.GetUpdateFrameWithInterval(
                frame,
                (uint)GetUpdateInterval(SystemUpdatePhase.GameSimulation),
                UpdateFrameBuckets)));

        using NativeArray<Entity> intersections = m_Query.ToEntityArray(Allocator.Temp);

        foreach (Entity intersection in intersections)
        {
            IntersectionTrafficRuntime runtime = EntityManager.GetComponentData<IntersectionTrafficRuntime>(intersection);
            if (runtime.PedestrianGroupMask == 0)
            {
                ResetHold(intersection, runtime);
                continue;
            }

            TrafficLights trafficLights = EntityManager.GetComponentData<TrafficLights>(intersection);
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
