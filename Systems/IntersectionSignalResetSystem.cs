using Cities2PedestrianTraffic.Components;
using Game;
using Game.Common;
using Game.Net;
using Unity.Collections;
using Unity.Entities;

namespace Cities2PedestrianTraffic.Systems;

/// <summary>
/// When the game marks an intersection Updated, discard all runtime data derived from the old
/// lane/group layout before vanilla rebuilds it. This prevents stale group ids after road edits.
/// </summary>
public partial class IntersectionSignalResetSystem : GameSystemBase
{
    private EntityQuery m_Query;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_Query = GetEntityQuery(
            ComponentType.ReadOnly<Updated>(),
            ComponentType.ReadOnly<TrafficLights>(),
            ComponentType.ReadOnly<SubLane>());
        RequireForUpdate(m_Query);
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> intersections = m_Query.ToEntityArray(Allocator.Temp);

        foreach (Entity intersection in intersections)
        {
            if (!EntityManager.Exists(intersection) || !EntityManager.HasBuffer<SubLane>(intersection))
            {
                continue;
            }

            DynamicBuffer<SubLane> subLanes = EntityManager.GetBuffer<SubLane>(intersection, true);
            for (int i = 0; i < subLanes.Length; i++)
            {
                Entity lane = subLanes[i].m_SubLane;
                if (EntityManager.Exists(lane) && EntityManager.HasComponent<FreeRightTurnLane>(lane))
                {
                    EntityManager.RemoveComponent<FreeRightTurnLane>(lane);
                }
            }

            if (EntityManager.HasComponent<IntersectionTrafficRuntime>(intersection))
            {
                EntityManager.RemoveComponent<IntersectionTrafficRuntime>(intersection);
            }
        }
    }
}
