using Unity.Entities;

namespace Cities2PedestrianTraffic.Components
{

/// <summary>
/// Runtime-only cache of pedestrian signal lanes for a configured intersection.
/// Rebuilt by IntersectionSignalSetupSystem whenever the network node is updated.
/// </summary>
[InternalBufferCapacity(4)]
public struct PedestrianSignalLaneRef : IBufferElementData
{
    public Entity Lane;
}
}
