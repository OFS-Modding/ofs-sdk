<p align="center">
  <img src="assets/logo.png" width="128" alt="OFS-Modding">
</p>

# OFS SDK

The public C# authoring SDK for Ore Factory Squad mods.

OFS SDK contains contracts, validation models, schemas, and API
compatibility tests. It does not install code into the game and does not contain
the native loader or managed runtime.

## Build

```powershell
dotnet build OFS.Sdk.sln -c Release
./eng/verify.ps1
```

## Package

```powershell
dotnet pack src/OFS.Sdk/OFS.Sdk.csproj -c Release -o artifacts/packages
```

The package ID is `OFS.Sdk`. During early development the API may change between
minor releases; breaking changes must update the API baseline and changelog.

## Repository boundaries

- Loader, runtime, installer, and Mod Hub: `OFS-Modding/ofs-loader`
- Exact Unity AssetBundle project: `OFS-Modding/ofs-asset-authoring`
- Reference mods: `OFS-Modding/ofs-example-mod` and `OFS-Modding/ofs-crash-and-drift`.

This is an unofficial community project and is not affiliated with the game's
developers or publisher. No game files are distributed here.
