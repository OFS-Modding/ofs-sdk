namespace OFS.Sdk;

/// <summary>Discovers and operates the game's live, server-authoritative ore nodes.</summary>
public interface IMiningNodeRegistry
{
    IReadOnlyList<IMiningNode> GetLoaded(bool activeOnly = true);
    bool TryGet(UnityObject gameObject, out IMiningNode node);
}

/// <summary>A live T_Item configured as a physical mining node.</summary>
public interface IMiningNode
{
    UnityObject GameObject { get; }
    UnityObject Component { get; }
    bool IsAlive { get; }
    int PieceCount { get; }
    IReadOnlyList<int> PieceHealth { get; }

    /// <summary>
    /// Breaks one remaining piece through the vanilla server path. This is intended for
    /// world actors such as automated miners; it does not credit a player inventory.
    /// </summary>
    bool MineNextPieceServer();
}
