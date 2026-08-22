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
/// When vanilla marks an intersection Updated, discard runtime state derived from the old
/// lane/group layout and mark only intersections with an effective mod configuration for a
/// single post-vanilla setup pass.
/// </summary>
public partial class IntersectionSignalResetSystem : GameSystemBase
{
    private EntityQuery m_Query;
    private EntityQuery m_AllTrafficLightsQuery;
    private EntityQuery m_GlobalConfigQuery;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_Query = GetEntityQuery(
            ComponentType.ReadOnly<Updated>(),
            ComponentType.ReadOnly<TrafficLights>(),
            ComponentType.ReadOnly<SubLane>(),
            ComponentType.Exclude<Deleted>(),
            ComponentType.Exclude<Destroyed>(),
            ComponentType.Exclude<Temp>());

        m_AllTrafficLightsQuery = GetEntityQuery(
            ComponentType.ReadOnly<TrafficLights>(),
            ComponentType.ReadOnly<SubLane>(),
            ComponentType.Exclude<Deleted>(),
            ComponentType.Exclude<Destroyed>(),
            ComponentType.Exclude<Temp>());

        m_GlobalConfigQuery = GetEntityQuery(ComponentType.ReadOnly<CityTrafficConfig>());
        RequireForUpdate(m_Query);
    }

    protected override void OnGameLoadingComplete(
        Colossal.Serialization.Entities.Purpose purpose,
        GameMode mode)
    {
        base.OnGameLoadingComplete(purpose, mode);

        if (purpose != Colossal.Serialization.Entities.Purpose.LoadGame &&
            purpose != Colossal.Serialization.Entities.Purpose.NewGame)
        {
            return;
        }

        // The old recurring setup system implicitly recovered persisted local configs after load.
        // With one-shot setup we explicitly queue configured intersections once here instead.
        IntersectionFeatureFlags globalFlags = GetGlobalFlags();
        using NativeArray<Entity> intersections = m_AllTrafficLightsQuery.ToEntityArray(Allocator.Temp);

        foreach (Entity intersection in intersections)
        {
            bool hasLocalConfig = EntityManager.HasComponent<IntersectionTrafficConfig>(intersection) &&
                                  !EntityManager.GetComponentData<IntersectionTrafficConfig>(intersection).IsEmpty;

            if (!hasLocalConfig && globalFlags == IntersectionFeatureFlags.None)
            {
                continue;
            }

            if (!EntityManager.HasComponent<Updated>(intersection))
            {
                EntityManager.AddComponent<Updated>(intersection);
            }
        }
    }

    protected override void OnUpdate()
    {
        // Read the city-wide setting once per reset batch, not once per intersection.
        IntersectionFeatureFlags globalFlags = GetGlobalFlags();
        using NativeArray<Entity> intersections = m_Query.ToEntityArray(Allocator.Temp);

        foreach (Entity intersection in intersections)
        {
            if (!EntityManager.Exists(intersection) || !EntityManager.HasBuffer<SubLane>(intersection))
            {
                continue;
            }

            ApplyGlobalOverride(intersection, globalFlags);

            bool hadRuntime = EntityManager.HasComponent<IntersectionTrafficRuntime>(intersection);
            if (hadRuntime)
            {
                CleanupLaneMarkers(intersection);
                EntityManager.RemoveComponent<IntersectionTrafficRuntime>(intersection);
            }

            bool needsSetup = HasEffectiveConfiguration(intersection);
            bool hasSetupMarker = EntityManager.HasComponent<IntersectionTrafficNeedsSetup>(intersection);

            if (needsSetup && !hasSetupMarker)
            {
                EntityManager.AddComponent<IntersectionTrafficNeedsSetup>(intersection);
            }
            else if (!needsSetup && hasSetupMarker)
            {
                EntityManager.RemoveComponent<IntersectionTrafficNeedsSetup>(intersection);
            }
        }
    }

    private IntersectionFeatureFlags GetGlobalFlags()
    {
        using NativeArray<Entity> configs = m_GlobalConfigQuery.ToEntityArray(Allocator.Temp);
        return configs.Length > 0
            ? EntityManager.GetComponentData<CityTrafficConfig>(configs[0]).Flags
            : IntersectionFeatureFlags.None;
    }

    private void CleanupLaneMarkers(Entity intersection)
    {
        DynamicBuffer<SubLane> subLanes = EntityManager.GetBuffer<SubLane>(intersection, true);
        for (int i = 0; i < subLanes.Length; i++)
        {
            Entity lane = subLanes[i].m_SubLane;
            if (EntityManager.Exists(lane) && EntityManager.HasComponent<FreeRightTurnLane>(lane))
            {
                EntityManager.RemoveComponent<FreeRightTurnLane>(lane);
            }
        }
    }

    private bool HasEffectiveConfiguration(Entity intersection)
    {
        if (EntityManager.HasComponent<Roundabout>(intersection))
        {
            return false;
        }

        if (EntityManager.HasComponent<IntersectionTrafficConfig>(intersection) &&
            !EntityManager.GetComponentData<IntersectionTrafficConfig>(intersection).IsEmpty)
        {
            return true;
        }

        return EntityManager.HasComponent<IntersectionTrafficGlobalOverride>(intersection) &&
               EntityManager.GetComponentData<IntersectionTrafficGlobalOverride>(intersection).Flags != IntersectionFeatureFlags.None;
    }

    private void ApplyGlobalOverride(Entity intersection, IntersectionFeatureFlags flags)
    {
        bool hasOverride = EntityManager.HasComponent<IntersectionTrafficGlobalOverride>(intersection);

        if (EntityManager.HasComponent<Roundabout>(intersection))
        {
            if (hasOverride)
            {
                EntityManager.RemoveComponent<IntersectionTrafficGlobalOverride>(intersection);
            }
            return;
        }

        if (flags == IntersectionFeatureFlags.None)
        {
            if (hasOverride)
            {
                EntityManager.RemoveComponent<IntersectionTrafficGlobalOverride>(intersection);
            }
            return;
        }

        IntersectionTrafficGlobalOverride globalOverride = new() { Flags = flags };
        if (hasOverride)
        {
            IntersectionTrafficGlobalOverride current =
                EntityManager.GetComponentData<IntersectionTrafficGlobalOverride>(intersection);
            if (current.Flags != flags)
            {
                EntityManager.SetComponentData(intersection, globalOverride);
            }
        }
        else
        {
            EntityManager.AddComponentData(intersection, globalOverride);
        }
    }
}
}
