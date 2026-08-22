using Unity.Entities;

namespace Cities2PedestrianTraffic.Components
{

/// <summary>
/// Derived state. Not serialized; rebuilt from the vanilla lane layout after load/rebuild.
/// </summary>
public struct IntersectionTrafficRuntime : IComponentData
{
    public ushort PedestrianGroupMask;
    public uint PedestrianHoldStartFrame;
    public bool PedestrianHoldActive;
    public bool FreeRightTurnActive;
}
}
