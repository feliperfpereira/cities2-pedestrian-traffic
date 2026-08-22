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
/// Runs after the vanilla TrafficLightInitializationSystem and makes two small, idempotent
/// amendments to the lane group masks of configured intersections.
/// </summary>
public partial class IntersectionSignalSetupSystem : GameSystemBase
{
    private EntityQuery m_Query;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_Query = GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadWrite<TrafficLights>(),
                ComponentType.ReadOnly<SubLane>(),
            },
            Any = new[]
            {
                ComponentType.ReadOnly<IntersectionTrafficConfig>(),
                ComponentType.ReadOnly<IntersectionTrafficGlobalOverride>(),
            },
            None = new[]
            {
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Temp>(),
            },
        });
        RequireForUpdate(m_Query);
    }

    protected override void OnUpdate()
    {
        using NativeArray<Entity> intersections = m_Query.ToEntityArray(Allocator.Temp);

        foreach (Entity intersection in intersections)
        {
            if (!EntityManager.Exists(intersection) ||
                !EntityManager.HasBuffer<SubLane>(intersection) ||
                !EntityManager.HasComponent<TrafficLights>(intersection))
            {
                continue;
            }

            bool hasLocalConfig = EntityManager.HasComponent<IntersectionTrafficConfig>(intersection);
            IntersectionTrafficConfig config = hasLocalConfig
                ? EntityManager.GetComponentData<IntersectionTrafficConfig>(intersection)
                : new IntersectionTrafficConfig(IntersectionFeatureFlags.None);

            if (EntityManager.HasComponent<IntersectionTrafficGlobalOverride>(intersection))
            {
                IntersectionTrafficGlobalOverride globalOverride =
                    EntityManager.GetComponentData<IntersectionTrafficGlobalOverride>(intersection);
                if (globalOverride.ExclusivePedestrianPhase)
                {
                    config.Set(IntersectionFeatureFlags.ExclusivePedestrianPhase, true);
                }

                if (globalOverride.FreeRightTurn)
                {
                    config.Set(IntersectionFeatureFlags.FreeRightTurn, true);
                }
            }
            TrafficLights trafficLights = EntityManager.GetComponentData<TrafficLights>(intersection);
            DynamicBuffer<SubLane> subLanes = EntityManager.GetBuffer<SubLane>(intersection, true);

            int groupCount = trafficLights.m_SignalGroupCount;
            ushort pedestrianGroupMask = 0;

            if (config.ExclusivePedestrianPhase)
            {
                pedestrianGroupMask = FindExistingPedestrianOnlyGroup(subLanes, groupCount);

                if (pedestrianGroupMask == 0 && HasPedestrianSignal(subLanes) && groupCount < 16)
                {
                    pedestrianGroupMask = (ushort)(1u << groupCount);
                    groupCount++;
                }

                // One dedicated group only. Existing multiple pedestrian-only groups are collapsed
                // to the first available bit to keep the feature predictable.
                pedestrianGroupMask = LowestBit(pedestrianGroupMask);

                if (pedestrianGroupMask != 0)
                {
                    SetPedestriansToExclusiveGroup(subLanes, pedestrianGroupMask);
                }
            }

            trafficLights.m_SignalGroupCount = (byte)groupCount;
            EntityManager.SetComponentData(intersection, trafficLights);

            UpdateRuntime(intersection, pedestrianGroupMask);
            ApplyFreeRightTurn(intersection, subLanes, groupCount, pedestrianGroupMask, config.FreeRightTurn);
        }
    }

    private ushort FindExistingPedestrianOnlyGroup(DynamicBuffer<SubLane> subLanes, int groupCount)
    {
        if (groupCount <= 0)
        {
            return 0;
        }

        ushort validMask = GroupMask(groupCount);
        ushort candidate = validMask;
        bool sawPedestrian = false;

        for (int i = 0; i < subLanes.Length; i++)
        {
            Entity lane = subLanes[i].m_SubLane;
            if (!EntityManager.HasComponent<LaneSignal>(lane))
            {
                continue;
            }

            LaneSignal signal = EntityManager.GetComponentData<LaneSignal>(lane);
            if (EntityManager.HasComponent<PedestrianLane>(lane))
            {
                sawPedestrian = true;
                candidate &= signal.m_GroupMask;
            }
            else
            {
                candidate &= (ushort)~signal.m_GroupMask;
            }
        }

        return sawPedestrian ? (ushort)(candidate & validMask) : (ushort)0;
    }

    private bool HasPedestrianSignal(DynamicBuffer<SubLane> subLanes)
    {
        for (int i = 0; i < subLanes.Length; i++)
        {
            Entity lane = subLanes[i].m_SubLane;
            if (EntityManager.HasComponent<PedestrianLane>(lane) && EntityManager.HasComponent<LaneSignal>(lane))
            {
                return true;
            }
        }

        return false;
    }

    private void SetPedestriansToExclusiveGroup(DynamicBuffer<SubLane> subLanes, ushort pedestrianGroupMask)
    {
        for (int i = 0; i < subLanes.Length; i++)
        {
            Entity lane = subLanes[i].m_SubLane;
            if (!EntityManager.HasComponent<PedestrianLane>(lane) || !EntityManager.HasComponent<LaneSignal>(lane))
            {
                continue;
            }

            LaneSignal signal = EntityManager.GetComponentData<LaneSignal>(lane);
            signal.m_GroupMask = pedestrianGroupMask;
            EntityManager.SetComponentData(lane, signal);
        }
    }

    private void ApplyFreeRightTurn(
        Entity intersection,
        DynamicBuffer<SubLane> subLanes,
        int groupCount,
        ushort pedestrianGroupMask,
        bool enabled)
    {
        ushort allGroups = GroupMask(groupCount);
        ushort allowedVehicleGroups = (ushort)(allGroups & ~pedestrianGroupMask);

        for (int i = 0; i < subLanes.Length; i++)
        {
            Entity lane = subLanes[i].m_SubLane;
            bool hasMarker = EntityManager.HasComponent<FreeRightTurnLane>(lane);
            FreeRightTurnLane previous = hasMarker
                ? EntityManager.GetComponentData<FreeRightTurnLane>(lane)
                : default;

            bool isRightTurn = false;
            if (EntityManager.HasComponent<CarLane>(lane))
            {
                CarLane carLane = EntityManager.GetComponentData<CarLane>(lane);
                isRightTurn = (carLane.m_Flags & (CarLaneFlags.TurnRight | CarLaneFlags.GentleTurnRight)) != 0;
            }

            if (!enabled || pedestrianGroupMask == 0 || !isRightTurn || !EntityManager.HasComponent<LaneSignal>(lane))
            {
                if (hasMarker)
                {
                    EntityManager.RemoveComponent<FreeRightTurnLane>(lane);
                }
                continue;
            }

            LaneSignal signal = EntityManager.GetComponentData<LaneSignal>(lane);

            // Remove groups we added previously to recover the current vanilla/base group mask.
            ushort previousAdded = hasMarker ? previous.YieldGroupMask : (ushort)0;
            ushort baseMask = (ushort)(signal.m_GroupMask & ~previousAdded);
            baseMask &= (ushort)~pedestrianGroupMask;

            ushort addedGroups = (ushort)(allowedVehicleGroups & ~baseMask);
            signal.m_GroupMask = (ushort)(baseMask | allowedVehicleGroups);
            EntityManager.SetComponentData(lane, signal);

            FreeRightTurnLane marker = new()
            {
                Intersection = intersection,
                YieldGroupMask = addedGroups,
            };

            if (hasMarker)
            {
                EntityManager.SetComponentData(lane, marker);
            }
            else
            {
                EntityManager.AddComponentData(lane, marker);
            }
        }
    }

    private void UpdateRuntime(Entity intersection, ushort pedestrianGroupMask)
    {
        IntersectionTrafficRuntime runtime = EntityManager.HasComponent<IntersectionTrafficRuntime>(intersection)
            ? EntityManager.GetComponentData<IntersectionTrafficRuntime>(intersection)
            : default;

        if (runtime.PedestrianGroupMask != pedestrianGroupMask)
        {
            runtime.PedestrianHoldActive = false;
            runtime.PedestrianHoldStartFrame = 0;
        }

        runtime.PedestrianGroupMask = pedestrianGroupMask;

        if (EntityManager.HasComponent<IntersectionTrafficRuntime>(intersection))
        {
            EntityManager.SetComponentData(intersection, runtime);
        }
        else
        {
            EntityManager.AddComponentData(intersection, runtime);
        }
    }

    private static ushort GroupMask(int groupCount)
    {
        if (groupCount <= 0)
        {
            return 0;
        }

        return groupCount >= 16
            ? ushort.MaxValue
            : (ushort)((1u << groupCount) - 1u);
    }

    private static ushort LowestBit(ushort value)
    {
        return value == 0 ? (ushort)0 : (ushort)(value & (ushort)(~value + 1));
    }
}
}
