using Unity.Entities;

namespace Cities2PedestrianTraffic.Components;

/// <summary>
/// Runtime marker for a right-turn lane that was added to signal groups by the mod.
/// The added groups must be Yield; the lane's original group remains normal Go.
/// </summary>
public struct FreeRightTurnLane : IComponentData
{
    public Entity Intersection;
    public ushort YieldGroupMask;
}
