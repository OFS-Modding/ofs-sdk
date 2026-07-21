# Computer apps and hired employees

SDK 0.3 adds high-level integration points for factory-computer applications
and the game's persistent hired-employee roster. Both APIs are main-thread APIs.

## Register a computer application

Register the application after the `Factory` scene loads and dispose the
registration when your mod unloads. The runtime creates the launcher entry when
the vanilla computer UI becomes available and removes it with the scene.

```csharp
private IComputerAppRegistration? _app;

private void OnSceneLoaded(SceneEvent scene)
{
    if (scene.Name != "Factory") return;
    _app?.Dispose();
    _app = context.GameplayUi.RegisterComputerApp(new ComputerAppDefinition(
        "dispatch",
        "DISPATCH",
        OpenDispatchPanel));
}
```

`Id` is local to the calling mod. The registration reports `IsMaterialized`
once a live vanilla launcher button exists. Use `Label` and `Visible` to update
that entry without recreating it.

## Query and reserve hired employees

`FindHiredVanillaEmployees(idleOnly: true)` returns immutable snapshots from
the vanilla `EmployeeManager`. It excludes fired employees and, when requested,
employees already assigned to an offsite or mod-defined assignment.

```csharp
var miner = context.Npcs.FindHiredVanillaEmployees(idleOnly: true)
    .FirstOrDefault(employee => employee.Type == VanillaEmployeeType.Miner);

if (miner is not null)
    context.Npcs.AssignHiredVanillaEmployeeServer(miner.QualifiedId, "my-mod:onsite");
```

Assignments are server-authoritative. Always release them on completion,
failure, scene teardown, and mod unload:

```csharp
context.Npcs.ReleaseHiredVanillaEmployeeServer(miner.QualifiedId);
```

Use the qualified ID returned by the snapshot; do not derive IDs from display
names. A custom assignment uses the same vanilla field as offsite contracts, so
an assigned employee automatically disappears from `idleOnly` queries.

## Coordinate dependent mods

Use `IModMessageBus` for process-local coordination rather than referencing
another mod's implementation assembly. Declare the dependency in
`manifest.json`, target the message to its mod ID, and make acquire/release
operations idempotent.

```csharp
context.Messages.Publish(
    "feature/lease/acquire",
    ReadOnlyMemory<byte>.Empty,
    new ModMessagePublishOptions(TargetModId: "dependency.mod"));
```

The message bus runs on Unity's main thread. It is not multiplayer transport;
use `INetworkApi` for data that must cross machines.
