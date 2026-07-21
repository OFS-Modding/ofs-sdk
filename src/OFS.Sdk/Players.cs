namespace OFS.Sdk;

/// <summary>Discovery and safe control of live network players.</summary>
public interface IPlayerApi
{
    IPlayer? Local { get; }
    IReadOnlyList<IPlayer> GetLoaded(bool activeOnly = true);
}

public interface IPlayer
{
    UnityObject GameObject { get; }
    UnityObject Component { get; }
    bool IsLocal { get; }
    bool IsInDigsite { get; set; }
    UnityTransform Transform { get; }
    void Teleport(UnityVector3 position, UnityQuaternion rotation);
}
