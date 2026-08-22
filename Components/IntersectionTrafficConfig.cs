using System;
using Colossal.Serialization.Entities;
using Unity.Entities;

namespace Cities2PedestrianTraffic.Components
{

[Flags]
public enum IntersectionFeatureFlags : uint
{
    None = 0,
    ExclusivePedestrianPhase = 1 << 0,
    FreeRightTurn = 1 << 1,
}

/// <summary>
/// Save-game data attached only to intersections explicitly configured by the player.
/// Runtime-derived signal masks are intentionally not serialized: the vanilla traffic-light
/// initialization recreates them after loading and our setup system reapplies these two flags.
/// </summary>
public struct IntersectionTrafficConfig : IComponentData, IQueryTypeParameter, ISerializable
{
    private const int CurrentSchemaVersion = 1;
    private IntersectionFeatureFlags m_Flags;

    public readonly IntersectionFeatureFlags Flags => m_Flags;
    public readonly bool ExclusivePedestrianPhase => (m_Flags & IntersectionFeatureFlags.ExclusivePedestrianPhase) != 0;
    public readonly bool FreeRightTurn => (m_Flags & IntersectionFeatureFlags.FreeRightTurn) != 0;
    public readonly bool IsEmpty => m_Flags == IntersectionFeatureFlags.None;

    public IntersectionTrafficConfig(IntersectionFeatureFlags flags)
    {
        if ((flags & IntersectionFeatureFlags.FreeRightTurn) != 0)
        {
            flags |= IntersectionFeatureFlags.ExclusivePedestrianPhase;
        }

        m_Flags = flags;
    }

    public void Set(IntersectionFeatureFlags flag, bool enabled)
    {
        if (enabled)
        {
            m_Flags |= flag;
        }
        else
        {
            m_Flags &= ~flag;
        }

        if (flag == IntersectionFeatureFlags.FreeRightTurn && enabled)
        {
            m_Flags |= IntersectionFeatureFlags.ExclusivePedestrianPhase;
        }

        if (flag == IntersectionFeatureFlags.ExclusivePedestrianPhase && !enabled)
        {
            m_Flags &= ~IntersectionFeatureFlags.FreeRightTurn;
        }
    }

    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
    {
        writer.Write(CurrentSchemaVersion);
        writer.Write((uint)m_Flags);
    }

    public void Deserialize<TReader>(TReader reader) where TReader : IReader
    {
        reader.Read(out int schemaVersion);
        reader.Read(out uint flags);

        m_Flags = (IntersectionFeatureFlags)flags;
        if ((m_Flags & IntersectionFeatureFlags.FreeRightTurn) != 0)
        {
            m_Flags |= IntersectionFeatureFlags.ExclusivePedestrianPhase;
        }

        _ = schemaVersion;
    }
}
}
