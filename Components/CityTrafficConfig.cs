using Colossal.Serialization.Entities;
using Unity.Entities;

namespace Cities2PedestrianTraffic.Components
{

public struct CityTrafficConfig : IComponentData, IQueryTypeParameter, ISerializable
{
    private const int CurrentSchemaVersion = 1;
    private IntersectionFeatureFlags m_Flags;

    public readonly bool ExclusivePedestrianPhase =>
        (m_Flags & IntersectionFeatureFlags.ExclusivePedestrianPhase) != 0;

    public readonly bool FreeRightTurn =>
        (m_Flags & IntersectionFeatureFlags.FreeRightTurn) != 0;

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

    public readonly IntersectionFeatureFlags Flags => m_Flags;

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
        _ = schemaVersion;
    }
}
}
