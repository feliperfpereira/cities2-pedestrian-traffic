using Unity.Entities;

namespace Cities2PedestrianTraffic.Components
{

/// <summary>
/// Runtime-only cache of right-turn signal lanes for a configured intersection.
/// YieldGroupMask contains only the signal groups added by the mod.
/// </summary>
[InternalBufferCapacity(4)]
public struct FreeRightTurnSignalLaneRef : IBufferElementData
{
    public Entity Lane;
    public ushort YieldGroupMask;
}
}
