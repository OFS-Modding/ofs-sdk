# Mod authoring

Mods implement `IOFSMod` and declare their entry point, SDK version,
capabilities, dependencies, and multiplayer policy in `manifest.json`.

Use high-level services whenever possible. `IUnsafeIl2CppApi` exists for missing
capabilities but has full process access and can terminate or corrupt the game.

`OFS-Modding/ofs-example-mod` covers the public services. The separate
`OFS-Modding/ofs-crash-and-drift` repository demonstrates input, hooks, physics,
and custom AssetBundle content.
