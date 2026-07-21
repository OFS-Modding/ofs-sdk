# Players and mining

OFS SDK 0.2.4 exposes stable wrappers for the local player and loaded vanilla
mining nodes. Mods should prefer these contracts over raw IL2CPP access.

## Local player

`context.Players.Local` returns the local `GamePlayer` once a gameplay host is
ready. `GetLoaded()` can be used when a mod needs to inspect all loaded player
objects.

```csharp
var player = context.Players.Local;
if (player is null)
    return;

player.IsInDigsite = true;
player.Teleport(
    new UnityVector3(10f, 5f, 20f),
    UnityQuaternion.Identity);
```

Teleportation uses the game's `GamePlayer.NetworkTeleport` method. A mod must
declare the `players.teleport` capability and should only teleport after the
save-load lifecycle has completed:

```csharp
context.Events.LoadCompleted += _ => ready = true;
```

## Mining nodes

`context.Content.MiningNodes.GetLoaded()` returns server-backed `T_Item` nodes.
Each handle exposes its current piece count and per-piece health.

```csharp
foreach (var node in context.Content.MiningNodes.GetLoaded())
{
    if (node.PieceHealth.Any(health => health > 0))
        node.MineNextPieceServer();
}
```

`MineNextPieceServer()` invokes the vanilla server break path with rewards and
damage disabled. It returns `false` when the node is gone, the server is not
active, or no intact piece remains. Call it only from the Unity main thread and
rate-limit repeated work.

## Runtime mining areas

`context.Content.MiningAreaSpawners` creates or attaches the vanilla networked
mining spawner. The vanilla component is a singleton, so a replacement must not
be created until its active instance is fully initialized.

Use both state fields exposed by `MiningAreaSpawnerDefinition`:

- `InitialCountsCalculated` becomes true after the vanilla node baseline is
  available.
- `IsRestoringFromSave` reports the persistent restore mode used by the game;
  it is diagnostic state and is not, by itself, a readiness signal.

A safe replacement flow is:

1. wait for `context.Events.LoadCompleted`;
2. require one active vanilla spawner with `InitialCountsCalculated == true`;
3. disable that spawner's GameObject;
4. create and spawn the mod-owned area;
5. remove the mod-owned area and reactivate the vanilla object before leaving.

Only a Mirror server or local host may create nodes. Runtime mining-area mods
should remain `multiplayer: "incompatible"` until their replication and
ownership behavior has been tested with multiple clients.
