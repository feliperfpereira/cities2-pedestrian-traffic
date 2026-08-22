using Cities2PedestrianTraffic.Components;
using Colossal.UI.Binding;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;

namespace Cities2PedestrianTraffic.Systems
{

public partial class TrafficUISystem : UISystemBase
{
    private const string BindingGroup = "Cities2PedestrianTraffic";

    private TrafficSelectionToolSystem m_ToolSystem = null!;
    private GetterValueBinding<string> m_StateBinding = null!;
    private EntityQuery m_GlobalConfigQuery;
    private EntityQuery m_TrafficLightQuery;
    private uint m_LastSelectionRevision;
    private bool m_LastToolActive;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_ToolSystem = World.GetOrCreateSystemManaged<TrafficSelectionToolSystem>();
        m_GlobalConfigQuery = GetEntityQuery(ComponentType.ReadWrite<CityTrafficConfig>());
        m_TrafficLightQuery = GetEntityQuery(
            ComponentType.ReadOnly<TrafficLights>(),
            ComponentType.ReadOnly<SubLane>(),
            ComponentType.Exclude<Deleted>(),
            ComponentType.Exclude<Destroyed>(),
            ComponentType.Exclude<Temp>());

        AddBinding(m_StateBinding = new GetterValueBinding<string>(BindingGroup, "State", GetState));
        AddBinding(new CallBinding<string, string>(BindingGroup, "Action", CallAction));

        m_LastSelectionRevision = m_ToolSystem.SelectionRevision;
        m_LastToolActive = m_ToolSystem.IsActive;
    }

    protected override void OnUpdate()
    {
        bool active = m_ToolSystem.IsActive;
        if (m_LastSelectionRevision != m_ToolSystem.SelectionRevision || m_LastToolActive != active)
        {
            m_LastSelectionRevision = m_ToolSystem.SelectionRevision;
            m_LastToolActive = active;
            m_StateBinding.Update();
        }
    }

    protected override void OnGameLoadingComplete(
        Colossal.Serialization.Entities.Purpose purpose,
        Game.GameMode mode)
    {
        base.OnGameLoadingComplete(purpose, mode);
        EnsureGlobalConfig();
        ApplyGlobalOverridesToAll();
        m_ToolSystem.ClearSelection();
        m_StateBinding?.Update();
    }

    private string CallAction(string action)
    {
        Mod.Log.Info($"UI action: {action}");

        switch (action)
        {
            case "toggleTool":
                if (m_ToolSystem.IsActive)
                {
                    m_ToolSystem.DisableTool();
                }
                else
                {
                    if (!m_ToolSystem.EnableTool())
                    {
                        Mod.Log.Warn("Traffic-light selection tool could not be enabled.");
                    }
                }
                break;

            case "togglePedestrian":
                ToggleFeature(IntersectionFeatureFlags.ExclusivePedestrianPhase);
                break;

            case "toggleRight":
                ToggleFeature(IntersectionFeatureFlags.FreeRightTurn);
                break;

            case "toggleCityPedestrian":
                ToggleCityFeature(IntersectionFeatureFlags.ExclusivePedestrianPhase);
                break;

            case "toggleCityRight":
                ToggleCityFeature(IntersectionFeatureFlags.FreeRightTurn);
                break;

            case "close":
                m_ToolSystem.DisableTool();
                m_ToolSystem.ClearSelection();
                break;
        }

        m_LastSelectionRevision = m_ToolSystem.SelectionRevision;
        m_LastToolActive = m_ToolSystem.IsActive;
        m_StateBinding.Update();
        return GetState();
    }

    private void ToggleCityFeature(IntersectionFeatureFlags flag)
    {
        Entity configEntity = EnsureGlobalConfig();
        CityTrafficConfig config = EntityManager.GetComponentData<CityTrafficConfig>(configEntity);
        bool currentlyEnabled = flag switch
        {
            IntersectionFeatureFlags.ExclusivePedestrianPhase => config.ExclusivePedestrianPhase,
            IntersectionFeatureFlags.FreeRightTurn => config.FreeRightTurn,
            _ => false,
        };

        config.Set(flag, !currentlyEnabled);
        EntityManager.SetComponentData(configEntity, config);
        Mod.Log.Info($"City-wide setting: pedestrian={config.ExclusivePedestrianPhase}, freeRight={config.FreeRightTurn}");
        ApplyGlobalOverridesToAll();
    }

    private void ToggleFeature(IntersectionFeatureFlags flag)
    {
        Entity intersection = m_ToolSystem.SelectedEntity;
        if (!IsValidSelection(intersection))
        {
            Mod.Log.Warn($"Ignoring {flag}: no valid traffic-light intersection is selected.");
            return;
        }

        IntersectionTrafficConfig config = EntityManager.HasComponent<IntersectionTrafficConfig>(intersection)
            ? EntityManager.GetComponentData<IntersectionTrafficConfig>(intersection)
            : new IntersectionTrafficConfig(IntersectionFeatureFlags.None);

        bool currentlyEnabled = flag switch
        {
            IntersectionFeatureFlags.ExclusivePedestrianPhase => config.ExclusivePedestrianPhase,
            IntersectionFeatureFlags.FreeRightTurn => config.FreeRightTurn,
            _ => false,
        };

        // Remove transient lane markers before asking vanilla to rebuild the node. The setup system
        // recreates only what remains enabled after TrafficLightInitializationSystem has run.
        CleanupRuntimeState(intersection);

        config.Set(flag, !currentlyEnabled);

        // Free-right is only safe when the pedestrian-only phase is also enabled.
        if (flag == IntersectionFeatureFlags.FreeRightTurn && !currentlyEnabled)
        {
            config.Set(IntersectionFeatureFlags.ExclusivePedestrianPhase, true);
        }

        // Do not leave the intersection in an unsafe combination if the pedestrian
        // phase is switched off while free-right remains enabled.
        if (flag == IntersectionFeatureFlags.ExclusivePedestrianPhase && currentlyEnabled && config.FreeRightTurn)
        {
            config.Set(IntersectionFeatureFlags.FreeRightTurn, false);
        }

        Mod.Log.Info($"Intersection {intersection.Index}: pedestrian={config.ExclusivePedestrianPhase}, freeRight={config.FreeRightTurn}");
        if (config.IsEmpty)
        {
            if (EntityManager.HasComponent<IntersectionTrafficConfig>(intersection))
            {
                EntityManager.RemoveComponent<IntersectionTrafficConfig>(intersection);
            }
        }
        else if (EntityManager.HasComponent<IntersectionTrafficConfig>(intersection))
        {
            EntityManager.SetComponentData(intersection, config);
        }
        else
        {
            EntityManager.AddComponentData(intersection, config);
        }

        if (!EntityManager.HasComponent<Updated>(intersection))
        {
            EntityManager.AddComponent<Updated>(intersection);
        }
    }

    private void CleanupRuntimeState(Entity intersection)
    {
        if (EntityManager.HasBuffer<SubLane>(intersection))
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

        if (EntityManager.HasComponent<IntersectionTrafficRuntime>(intersection))
        {
            EntityManager.RemoveComponent<IntersectionTrafficRuntime>(intersection);
        }
    }

    private string GetState()
    {
        Entity selected = m_ToolSystem.SelectedEntity;
        bool hasSelection = IsValidSelection(selected);
        bool pedestrian = false;
        bool freeRight = false;

        if (hasSelection)
        {
            IntersectionTrafficConfig config = GetEffectiveConfig(selected);
            pedestrian = config.ExclusivePedestrianPhase;
            freeRight = config.FreeRightTurn;
        }

        CityTrafficConfig cityConfig = GetGlobalConfig();
        int selectedIndex = hasSelection ? selected.Index : -1;
        return "{" +
               $"\"ready\":{JsonBool(m_ToolSystem.IsReady)}," +
               $"\"toolActive\":{JsonBool(m_ToolSystem.IsActive)}," +
               $"\"hasSelection\":{JsonBool(hasSelection)}," +
               $"\"selectedIndex\":{selectedIndex}," +
               $"\"pedestrian\":{JsonBool(pedestrian)}," +
               $"\"freeRight\":{JsonBool(freeRight)}," +
               $"\"cityPedestrian\":{JsonBool(cityConfig.ExclusivePedestrianPhase)}," +
               $"\"cityFreeRight\":{JsonBool(cityConfig.FreeRightTurn)}" +
               "}";
    }

    private Entity EnsureGlobalConfig()
    {
        using NativeArray<Entity> entities = m_GlobalConfigQuery.ToEntityArray(Allocator.Temp);
        if (entities.Length > 0)
        {
            return entities[0];
        }

        Entity entity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(entity, new CityTrafficConfig());
        return entity;
    }

    private CityTrafficConfig GetGlobalConfig()
    {
        using NativeArray<Entity> entities = m_GlobalConfigQuery.ToEntityArray(Allocator.Temp);
        return entities.Length > 0
            ? EntityManager.GetComponentData<CityTrafficConfig>(entities[0])
            : default;
    }

    private void ApplyGlobalOverridesToAll()
    {
        IntersectionFeatureFlags flags = GetGlobalConfig().Flags;
        using NativeArray<Entity> intersections = m_TrafficLightQuery.ToEntityArray(Allocator.Temp);

        foreach (Entity intersection in intersections)
        {
            if (!EntityManager.Exists(intersection) ||
                EntityManager.HasComponent<Roundabout>(intersection))
            {
                continue;
            }

            bool hasOverride = EntityManager.HasComponent<IntersectionTrafficGlobalOverride>(intersection);
            bool changed = false;

            if (flags == IntersectionFeatureFlags.None)
            {
                if (hasOverride)
                {
                    EntityManager.RemoveComponent<IntersectionTrafficGlobalOverride>(intersection);
                    changed = true;
                }
            }
            else
            {
                IntersectionTrafficGlobalOverride globalOverride = new() { Flags = flags };
                if (hasOverride)
                {
                    IntersectionTrafficGlobalOverride current =
                        EntityManager.GetComponentData<IntersectionTrafficGlobalOverride>(intersection);
                    if (current.Flags != flags)
                    {
                        EntityManager.SetComponentData(intersection, globalOverride);
                        changed = true;
                    }
                }
                else
                {
                    EntityManager.AddComponentData(intersection, globalOverride);
                    changed = true;
                }
            }

            if (changed && !EntityManager.HasComponent<Updated>(intersection))
            {
                EntityManager.AddComponent<Updated>(intersection);
            }
        }
    }

    private IntersectionTrafficConfig GetEffectiveConfig(Entity intersection)
    {
        IntersectionTrafficConfig config = EntityManager.HasComponent<IntersectionTrafficConfig>(intersection)
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

        return config;
    }

    private bool IsValidSelection(Entity entity)
    {
        return entity != Entity.Null &&
               EntityManager.Exists(entity) &&
               EntityManager.HasComponent<TrafficLights>(entity);
    }

    private static string JsonBool(bool value) => value ? "true" : "false";
}
}
