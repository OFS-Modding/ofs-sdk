# OFS.Sdk

Public, loader-independent contracts for Ore Factory Squad mods.

```csharp
public sealed class MyMod : IOFSMod
{
    public void Load(IModContext context)
    {
        context.Events.MainMenuReady += menu =>
        {
            var panel = menu.AddPanel(new("panel", "MY MOD"));
            panel.AddText(new("state", "READY"));
            panel.AddButton(new("close", "CLOSE", panel.Close));
            menu.AddButton(new("open", "MY MOD", panel.Show));
        };
    }
}
```

The runtime currently provides:

- isolated assembly discovery through `manifest.json`;
- per-mod logging and configuration directories;
- typed runtime/build facts through `context.Runtime`, including the exact game
  fingerprint, Unity/IL2CPP ABI and live main-thread state;
- Unity main-thread scheduling and lifecycle events;
- styled main-menu button registration;
- common Unity object operations;
- active/inactive loaded-component enumeration for inspecting and modifying
  existing vanilla objects without raw IL2CPP traversal;
- world spawning, reusable NPC definitions/visual variants, managed NPC
  behaviors, local animation/FollowerEntity navigation, a typed controller for
  the vanilla `T_Employee` FSM, Mirror-aware network spawning and mechanics;
- owner-aware vanilla `Interactable` routing and localized branching dialogues;
- verified dependency-aware AssetBundle sets and reusable generic entities with
  visual variants, managed behaviors and optional vanilla interaction;
- host-authoritative Mirror spawning of those entity definitions with prefab
  leases, `NetworkIdentity`, `netId` and deterministic cleanup;
- owner-scoped binary Mirror channels for client-to-server commands, targeted
  replies and broadcasts with authentication and payload limits;
- typed server-authoritative replicated state with automatic snapshots,
  monotonic revisions, JSON defaults and custom binary serializers;
- typed client-to-server RPCs with authenticated peers, correlation ids,
  directed success/error responses, cancellation and main-thread timeouts;
- serializable Mirror object references, server/client `netId` lookup and
  owner-scoped target validation for entity-directed commands;
- declarative RPC authorization and per-connection token-bucket rate limiting
  with fail-safe defaults and explicit unlimited opt-out;
- owner-aware Unity Input System actions for keyboard/mouse and UI capture;
- typed, name-resolved IL2CPP method detours with rooted managed delegates;
- deterministic multiplayer profiles, typed mismatches and consent-based
  restart remediation plans;
- typed item/building/recipe content access, full item visual/mining metadata,
  item filters/building categories,
  owner-scoped I2 localization, AssetBundles and per-save JSON sidecars with
  declarative transactional schema migrations and rollback;
- catalog manifests, semantic-version ranges and dependency resolution;
- pure dependency-aware mod profile resolution;
- durable load journals plus no-per-callback-I/O breadcrumbs for frame,
  Mirror handlers and declarative IL2CPP detours;
- an explicit unsafe IL2CPP escape hatch, including exact overload lookup,
  class/method/field metadata, aligned value/reference invocation arguments,
  static-field access, boxing and generic reference arrays.

The repository pins every public/protected contract in
`PublicAPI.Shipped.txt`. CI rejects unreviewed changes to signatures, nullability,
enum values, optional defaults and generic constraints.

Mods execute with full process trust. `IUnsafeIl2CppApi` can crash or corrupt the
game when given invalid pointers or signatures. Prefer high-level SDK services
where they exist.

OFS.Sdk is licensed under the MIT License.
