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
/// Runs immediately after vanilla on the same UpdateFrame slice. Uses a setup-time cache of the
/// right-turn lanes that actually need Yield handling, avoiding a full SubLane scan per junction.
/// </summary>
public partial class FreeRightTurnYieldSystem : GameSystemBase
{
    private const int UpdateFrameBuckets = 16;

    private EntityQuery m_Query;
    private SimulationSystem m_SimulationSystem = null!;

    public override int GetUpdateInterval(SystemUpdatePhase phase) => 4;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        m_Query = GetEntityQuery(
            ComponentType.ReadOnly<IntersectionTrafficRuntime>(),
            ComponentType.ReadOnly<TrafficLights>(),
            ComponentType.ReadOnly<FreeRightTurnSignalLaneRef>(),
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
            if (!runtime.FreeRightTurnActive)
            {
                continue;
            }

            TrafficLights trafficLights = EntityManager.GetComponentData<TrafficLights>(intersection);
            if (trafficLights.m_CurrentSignalGroup == 0)
            {
                continue;
            }

            ushort currentGroup = (ushort)(1u << (trafficLights.m_CurrentSignalGroup - 1));
            DynamicBuffer<FreeRightTurnSignalLaneRef> rightTurnLanes =
                EntityManager.GetBuffer<FreeRightTurnSignalLaneRef>(intersection, true);

            for (int i = 0; i < rightTurnLanes.Length; i++)
            {
                FreeRightTurnSignalLaneRef cachedLane = rightTurnLanes[i];
                if ((cachedLane.YieldGroupMask & currentGroup) == 0)
                {
                    continue;
                }

                Entity lane = cachedLane.Lane;
                if (!EntityManager.Exists(lane) || !EntityManager.HasComponent<LaneSignal>(lane))
                {
                    continue;
                }

                LaneSignal signal = EntityManager.GetComponentData<LaneSignal>(lane);
                if (signal.m_Signal == LaneSignalType.Go)
                {
                    signal.m_Signal = LaneSignalType.Yield;
                    EntityManager.SetComponentData(lane, signal);
                }
            }
        }
    }
}
}
