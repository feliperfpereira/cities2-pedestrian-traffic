using Cities2PedestrianTraffic.Components;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace Cities2PedestrianTraffic.Systems
{

/// <summary>
/// Vanilla has already evaluated the signal by the time this runs. If a right-turn lane is green
/// only because the mod added it to the current group, downgrade Go to Yield. In the dedicated
/// pedestrian group the lane is not a member at all, so vanilla leaves it Stop.
/// </summary>
public partial class FreeRightTurnYieldSystem : GameSystemBase
{
    private EntityQuery m_Query;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_Query = GetEntityQuery(
            ComponentType.ReadOnly<FreeRightTurnLane>(),
            ComponentType.ReadWrite<LaneSignal>(),
            ComponentType.Exclude<Deleted>(),
            ComponentType.Exclude<Destroyed>(),
            ComponentType.Exclude<Temp>());
        RequireForUpdate(m_Query);
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> lanes = m_Query.ToEntityArray(Allocator.Temp);

        foreach (Entity lane in lanes)
        {
            FreeRightTurnLane freeTurn = EntityManager.GetComponentData<FreeRightTurnLane>(lane);
            if (freeTurn.Intersection == Entity.Null ||
                !EntityManager.Exists(freeTurn.Intersection) ||
                !EntityManager.HasComponent<TrafficLights>(freeTurn.Intersection))
            {
                continue;
            }

            TrafficLights trafficLights = EntityManager.GetComponentData<TrafficLights>(freeTurn.Intersection);
            if (trafficLights.m_CurrentSignalGroup == 0)
            {
                continue;
            }

            ushort currentGroup = (ushort)(1u << (trafficLights.m_CurrentSignalGroup - 1));
            if ((freeTurn.YieldGroupMask & currentGroup) == 0)
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
