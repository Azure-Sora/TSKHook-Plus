param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $root 'TSKHook.csproj') -c Release "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed' }
Copy-Item -LiteralPath (Join-Path $root 'bin/Release/net6.0/TSKHook.dll') -Destination (Join-Path $PSScriptRoot 'release/TSKHook.dll')
$legacyCore = Join-Path $PSScriptRoot 'release/TSKHook.Translation.Core.dll'
if (Test-Path -LiteralPath $legacyCore) { Remove-Item -LiteralPath $legacyCore }
Write-Host 'Single plugin DLL staged. Translation data is published separately by TSKHook-UITranslations.'
