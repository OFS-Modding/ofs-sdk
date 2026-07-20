# Authoring

Reference `OFS.Sdk`, implement `IOFSMod`, and declare the entry point and
capabilities in `manifest.json`. Start with `OFS-Modding/ofs-example-mod`.

Prefer high-level services. Unsafe IL2CPP access has full process trust and can
crash the game when signatures or layouts are wrong.
