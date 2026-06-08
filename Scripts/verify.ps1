# C# compile check for Rougelite_Idle (run from project root)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build Rougelite_Idle.csproj --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "dotnet build OK"
} finally {
    Pop-Location
}
