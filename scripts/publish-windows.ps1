param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = ".\publish\AutoTest.ErpAutomation"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\AutoTest.ErpAutomation\AutoTest.ErpAutomation.csproj"
$outputPath = Join-Path $repoRoot $Output

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $outputPath

Write-Host "Published: $outputPath"
