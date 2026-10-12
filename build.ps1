param([Parameter(Mandatory = $true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $references = Join-Path (Resolve-Path $GamePath).Path 'MelonLoader'
    dotnet build src/SlopHero.Mod -c Release "-p:GameReferences=$references"
    if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
    dotnet run --project tests/SlopHero.Core.Checks -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    New-Item -ItemType Directory -Force dist | Out-Null
    Copy-Item src/SlopHero.Mod/bin/Release/net6.0/SlopHero.dll dist/SlopHero.dll
    Write-Host "Built dist/SlopHero.dll. Close the game, then copy it to your Mods folder."
} finally { Pop-Location }
