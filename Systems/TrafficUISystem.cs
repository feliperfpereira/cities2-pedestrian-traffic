using Cities2PedestrianTraffic.Components;
using Colossal.UI.Binding;
using Game.Common;
using Game.Net;
using Game.UI;
using Unity.Entities;

namespace Cities2PedestrianTraffic.Systems;

public partial class TrafficUISystem : UISystemBase
{
    private const string BindingGroup = "Cities2PedestrianTraffic";

    private TrafficSelectionToolSystem m_ToolSystem = null!;
    private GetterValueBinding<string> m_StateBinding = null!;
    private uint m_LastSelectionRevision;
    private bool m_LastToolActive;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_ToolSystem = World.GetOrCreateSystemManaged<TrafficSelectionToolSystem>();

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
        m_ToolSystem.ClearSelection();
        m_StateBinding?.Update();
    }

    private string CallAction(string action)
    {
        switch (action)
        {
            case "toggleTool":
                if (m_ToolSystem.IsActive)
                {
                    m_ToolSystem.DisableTool();
                }
                else
                {
                    m_ToolSystem.EnableTool();
                }
                break;

            case "togglePedestrian":
                ToggleFeature(IntersectionFeatureFlags.ExclusivePedestrianPhase);
                break;

            case "toggleRight":
                ToggleFeature(IntersectionFeatureFlags.FreeRightTurn);
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

    private void ToggleFeature(IntersectionFeatureFlags flag)
    {
        Entity intersection = m_ToolSystem.SelectedEntity;
        if (!IsValidSelection(intersection))
        {
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

        if (hasSelection && EntityManager.HasComponent<IntersectionTrafficConfig>(selected))
        {
            IntersectionTrafficConfig config = EntityManager.GetComponentData<IntersectionTrafficConfig>(selected);
            pedestrian = config.ExclusivePedestrianPhase;
            freeRight = config.FreeRightTurn;
        }

        int selectedIndex = hasSelection ? selected.Index : -1;
        return "{" +
               $"\"ready\":{JsonBool(m_ToolSystem.IsReady)}," +
               $"\"toolActive\":{JsonBool(m_ToolSystem.IsActive)}," +
               $"\"hasSelection\":{JsonBool(hasSelection)}," +
               $"\"selectedIndex\":{selectedIndex}," +
               $"\"pedestrian\":{JsonBool(pedestrian)}," +
               $"\"freeRight\":{JsonBool(freeRight)}" +
               "}";
    }

    private bool IsValidSelection(Entity entity)
    {
        return entity != Entity.Null &&
               EntityManager.Exists(entity) &&
               EntityManager.HasComponent<TrafficLights>(entity);
    }

    private static string JsonBool(bool value) => value ? "true" : "false";
}
