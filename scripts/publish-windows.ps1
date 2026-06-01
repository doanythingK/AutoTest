param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = ".\publish\AutoTest.ErpAutomation"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\AutoTest.ErpAutomation\AutoTest.ErpAutomation.csproj"
$outputPath = Join-Path $repoRoot $Output
$sourceRevision = ""

try {
    $sourceRevision = (& git -C $repoRoot rev-parse --short=12 HEAD).Trim()
} catch {
    $sourceRevision = ""
}

$publishArgs = @(
    "publish", $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "false",
    "-o", $outputPath
)

if (-not [string]::IsNullOrWhiteSpace($sourceRevision)) {
    $publishArgs += "-p:SourceRevisionId=$sourceRevision"
}

dotnet @publishArgs

Write-Host "Published: $outputPath"
if (-not [string]::IsNullOrWhiteSpace($sourceRevision)) {
    Write-Host "Source revision: $sourceRevision"
}
