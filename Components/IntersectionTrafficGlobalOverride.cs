using Unity.Entities;

namespace Cities2PedestrianTraffic.Components
{

/// <summary>
/// Runtime-only city-wide flags applied on top of a saved per-intersection config.
/// The city setting itself is serialized by CityTrafficConfig.
/// </summary>
public struct IntersectionTrafficGlobalOverride : IComponentData, IQueryTypeParameter
{
    public IntersectionFeatureFlags Flags;

    public readonly bool ExclusivePedestrianPhase =>
        (Flags & IntersectionFeatureFlags.ExclusivePedestrianPhase) != 0;

    public readonly bool FreeRightTurn =>
        (Flags & IntersectionFeatureFlags.FreeRightTurn) != 0;
}
}
