using System.Reflection;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Cities2PedestrianTraffic.Systems;

/// <summary>
/// Tiny selection tool built on the game's traffic-light net upgrade tool. It never edits lane
/// configuration itself; a left click only publishes the selected traffic-light node to the UI.
/// </summary>
public partial class TrafficSelectionToolSystem : NetToolSystem
{
    public override string toolID => "Cities2PedestrianTraffic.Tool";

    private NativeList<ControlPoint> m_ParentControlPoints;
    private NativeReference<AppliedUpgrade> m_ParentAppliedUpgrade;
    private EntityQuery m_PlaceableNetDataQuery;
    private Entity m_TrafficLightPrefabEntity = Entity.Null;
    private Entity m_RaycastResult = Entity.Null;

    public Entity SelectedEntity { get; private set; } = Entity.Null;
    public uint SelectionRevision { get; private set; }
    public bool IsActive => m_ToolSystem != null && m_ToolSystem.activeTool == this;
    public bool IsReady => m_TrafficLightPrefabEntity != Entity.Null;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_ParentControlPoints = GetControlPoints(out JobHandle _);

        FieldInfo? appliedUpgradeField = typeof(NetToolSystem).GetField(
            "m_AppliedUpgrade",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (appliedUpgradeField?.GetValue(this) is NativeReference<AppliedUpgrade> appliedUpgrade)
        {
            m_ParentAppliedUpgrade = appliedUpgrade;
        }
        else
        {
            Mod.Log.Error("Could not access NetToolSystem.m_AppliedUpgrade; selection tool disabled.");
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        JobHandle result = base.OnUpdate(inputDeps);

        // This mod deliberately has no right-click action; never expose traffic-light removal here.
        secondaryApplyAction.shouldBeEnabled = false;

        if (m_ParentControlPoints.IsCreated && m_ParentControlPoints.Length >= 4)
        {
            m_RaycastResult = m_ParentControlPoints[m_ParentControlPoints.Length - 3].m_OriginalEntity;
        }
        else
        {
            m_RaycastResult = Entity.Null;
        }

        applyAction.shouldBeEnabled = IsValidTrafficLight(m_RaycastResult);

        if (applyAction.WasReleasedThisFrame() && m_ParentAppliedUpgrade.IsCreated)
        {
            Entity entity = m_ParentAppliedUpgrade.Value.m_Entity;
            if (IsValidTrafficLight(entity))
            {
                SelectedEntity = entity;
                SelectionRevision++;
            }
        }

        return result;
    }

    protected override void OnGameLoadingComplete(
        Colossal.Serialization.Entities.Purpose purpose,
        Game.GameMode mode)
    {
        base.OnGameLoadingComplete(purpose, mode);

        m_TrafficLightPrefabEntity = Entity.Null;
        m_RaycastResult = Entity.Null;
        SelectedEntity = Entity.Null;
        SelectionRevision++;

        if (m_PlaceableNetDataQuery == default)
        {
            m_PlaceableNetDataQuery = GetEntityQuery(ComponentType.ReadOnly<PlaceableNetData>());
        }

        using NativeArray<Entity> entities = m_PlaceableNetDataQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<PlaceableNetData> data = m_PlaceableNetDataQuery.ToComponentDataArray<PlaceableNetData>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if ((data[i].m_SetUpgradeFlags.m_General & CompositionFlags.General.TrafficLights) == 0)
            {
                continue;
            }

            if (m_PrefabSystem.TryGetPrefab(entities[i], out PrefabBase prefabBase) && prefabBase is NetPrefab)
            {
                m_TrafficLightPrefabEntity = entities[i];
                break;
            }
        }

        if (m_TrafficLightPrefabEntity == Entity.Null)
        {
            Mod.Log.Error("Traffic-light net prefab was not found; selection tool cannot be enabled.");
        }
    }

    protected override bool GetAllowApply()
    {
        return IsValidTrafficLight(m_RaycastResult);
    }

    public override bool TrySetPrefab(PrefabBase prefab) => false;
    public override PrefabBase GetPrefab() => null;

    public bool EnableTool()
    {
        if (m_TrafficLightPrefabEntity == Entity.Null ||
            !m_PrefabSystem.TryGetPrefab(m_TrafficLightPrefabEntity, out NetPrefab netPrefab))
        {
            return false;
        }

        prefab = netPrefab;
        underground = m_ToolSystem.activeTool.requireUnderground;
        m_ToolSystem.activeTool = this;
        return true;
    }

    public void DisableTool()
    {
        if (m_ToolSystem.activeTool == this)
        {
            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }
    }

    public void ClearSelection()
    {
        if (SelectedEntity != Entity.Null)
        {
            SelectedEntity = Entity.Null;
            SelectionRevision++;
        }
    }

    private bool IsValidTrafficLight(Entity entity)
    {
        if (entity == Entity.Null || !EntityManager.Exists(entity))
        {
            return false;
        }

        if (EntityManager.HasComponent<Roundabout>(entity) || !EntityManager.HasComponent<TrafficLights>(entity))
        {
            return false;
        }

        TrafficLights lights = EntityManager.GetComponentData<TrafficLights>(entity);
        return (lights.m_Flags & TrafficLightFlags.MoveableBridge) == 0;
    }
}
