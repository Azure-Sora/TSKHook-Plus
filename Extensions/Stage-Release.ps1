param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $root 'TSKHook.csproj') -c Release "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed' }
foreach ($name in @('TSKHook.dll', 'TSKHook.Translation.Core.dll')) {
    Copy-Item -LiteralPath (Join-Path $root "bin/Release/net6.0/$name") -Destination (Join-Path $PSScriptRoot "release/$name")
}
Write-Host 'Release DLLs staged. Translation data is published separately by TSKHook-UITranslations.'
