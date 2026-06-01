param(
    [switch]$Quiet
)

$ErrorActionPreference = "SilentlyContinue"
$root = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $root "data/logs"

$pidFiles = @(
    Join-Path $logDir "api.pid",
    Join-Path $logDir "web.pid"
)

foreach ($pidFile in $pidFiles) {
    if (Test-Path -LiteralPath $pidFile) {
        $processId = Get-Content -LiteralPath $pidFile | Select-Object -First 1
        if ($processId) {
            $children = Get-CimInstance Win32_Process |
                Where-Object { $_.ParentProcessId -eq [int]$processId } |
                Select-Object -ExpandProperty ProcessId
            foreach ($child in $children) {
                Stop-Process -Id $child -Force
            }
            Stop-Process -Id ([int]$processId) -Force
        }
        Remove-Item -LiteralPath $pidFile -Force
    }
}

$ports = @(8080, 5173)
for ($i = 0; $i -lt 5; $i++) {
    $owners = Get-NetTCPConnection -LocalPort $ports -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique

    if (-not $owners) {
        break
    }

    foreach ($owner in $owners) {
        Stop-Process -Id $owner -Force
    }

    Start-Sleep -Milliseconds 500
}

if (-not $Quiet) {
    Write-Host "Stopped non-Docker Veritas Workbench processes on ports 8080 and 5173."
}
