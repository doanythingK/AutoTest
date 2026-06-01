param(
    [int]$Port = 9222,
    [string]$PublishedApp = ".\publish\AutoTest.ErpAutomation\AutoTest.ErpAutomation.exe",
    [switch]$WarnOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishedAppPath = Join-Path $repoRoot $PublishedApp
$debugEndpoint = "http://127.0.0.1:$Port"
$loginUrl = "https://ibcenter.co.kr/erp/erp/erplogin/erplogin_dispatch.jsp"
$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Write-Check {
    param(
        [string]$Status,
        [string]$Message
    )

    Write-Host ("[{0}] {1}" -f $Status, $Message)
}

function Add-Failure {
    param([string]$Message)
    $failures.Add($Message) | Out-Null
    Write-Check "FAIL" $Message
}

function Add-Warning {
    param([string]$Message)
    $warnings.Add($Message) | Out-Null
    Write-Check "WARN" $Message
}

function Find-ChromePath {
    $candidates = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
    )

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    $command = Get-Command chrome.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    return $null
}

Write-Host "ERP live automation readiness check"
Write-Host "Repository: $repoRoot"
Write-Host "Chrome CDP endpoint: $debugEndpoint"
Write-Host ""

if (Test-Path $publishedAppPath) {
    Write-Check "PASS" "Published WPF app found: $publishedAppPath"
} else {
    Add-Warning "Published WPF app not found: $publishedAppPath. Run scripts\publish-windows.ps1 before copying to an operation PC."
}

$desktopRuntime = (& dotnet --list-runtimes 2>$null) | Where-Object { $_ -match "^Microsoft\.WindowsDesktop\.App 8\." } | Select-Object -First 1
if ($desktopRuntime) {
    Write-Check "PASS" ".NET 8 Desktop Runtime found: $desktopRuntime"
} else {
    Add-Failure ".NET 8 Desktop Runtime was not found. Install Microsoft.WindowsDesktop.App 8.x runtime before running the WPF app."
}

$chromePath = Find-ChromePath
if ($chromePath) {
    Write-Check "PASS" "Chrome found: $chromePath"
} else {
    Add-Failure "Chrome executable was not found."
}

try {
    $version = Invoke-RestMethod -Uri "$debugEndpoint/json/version" -TimeoutSec 2
    if ($version.webSocketDebuggerUrl) {
        Write-Check "PASS" "Chrome remote debugging connected: $($version.Browser)"
    } else {
        Add-Failure "Chrome responded on port $Port but webSocketDebuggerUrl is missing."
    }
} catch {
    Add-Failure "Chrome remote debugging port $Port is not reachable. Start Chrome with --remote-debugging-port=$Port before running automation."
    if ($chromePath) {
        Write-Host "Manual launch command:"
        Write-Host "`"$chromePath`" --remote-debugging-port=$Port --profile-directory=Default `"$loginUrl`""
    }
}

try {
    $tabs = Invoke-RestMethod -Uri "$debugEndpoint/json/list" -TimeoutSec 2
    $visibleTabs = @($tabs | Where-Object { $_.type -eq "page" })
    Write-Check "INFO" ("Open CDP page tabs: {0}" -f $visibleTabs.Count)

    $erpTabs = @($visibleTabs | Where-Object { $_.url -like "https://ibcenter.co.kr/*" })
    if ($erpTabs.Count -gt 0) {
        Write-Check "PASS" ("ERP tabs visible through CDP: {0}" -f $erpTabs.Count)
    } else {
        Add-Warning "No ERP tab is currently visible through CDP. The WPF app can open the ERP login page during automation."
    }
} catch {
    Add-Warning "Could not read Chrome tab list from CDP."
}

Write-Host ""
Write-Host "Summary"
Write-Host ("- Failures: {0}" -f $failures.Count)
Write-Host ("- Warnings: {0}" -f $warnings.Count)

if ($failures.Count -eq 0) {
    Write-Check "PASS" "Live automation environment is ready for WPF execution checks."
    exit 0
}

if ($WarnOnly) {
    Write-Check "WARN" "Readiness check found failures, but WarnOnly was set."
    exit 0
}

exit 1
