param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot

Push-Location $repository
try {
    dotnet format OFS.Sdk.sln --no-restore --verify-no-changes
    if ($LASTEXITCODE -ne 0) { throw 'Formatting verification failed.' }
    dotnet build OFS.Sdk.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'SDK build failed.' }
    dotnet run --project tests/OFS.Sdk.ApiSurface -c Release --no-build -- `
        verify src/OFS.Sdk/PublicAPI.Shipped.txt
    if ($LASTEXITCODE -ne 0) { throw 'Public API baseline verification failed.' }
    dotnet pack src/OFS.Sdk/OFS.Sdk.csproj -c Release --no-build -o artifacts/packages
    if ($LASTEXITCODE -ne 0) { throw 'SDK package build failed.' }
    Write-Host 'OFS SDK verification passed.'
}
finally {
    Pop-Location
}
