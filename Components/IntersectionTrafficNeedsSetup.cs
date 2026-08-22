using Unity.Entities;

namespace Cities2PedestrianTraffic.Components
{

/// <summary>
/// Transient marker added before vanilla rebuilds a traffic-light node and consumed once
/// afterwards by IntersectionSignalSetupSystem. This keeps expensive lane-mask work out of
/// the steady-state simulation loop.
/// </summary>
public struct IntersectionTrafficNeedsSetup : IComponentData, IQueryTypeParameter
{
}
}
