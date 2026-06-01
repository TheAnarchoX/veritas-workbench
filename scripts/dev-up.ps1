param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $root "data/logs"
$dbPath = Join-Path $root "data/veritas-dev.db"
$storagePath = Join-Path $root "data/storage"
$workerDir = Join-Path $root "workers/forensics"

& (Join-Path $PSScriptRoot "dev-down.ps1") -Quiet
New-Item -ItemType Directory -Force -Path $logDir, $storagePath | Out-Null

if (-not $NoBuild) {
    dotnet build (Join-Path $root "Veritas.Workbench.slnx") --no-restore
}

$env:ASPNETCORE_URLS = "http://127.0.0.1:8080"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__Provider = "Sqlite"
$env:ConnectionStrings__Sqlite = "Data Source=$dbPath"
$env:Database__EnsureCreated = "true"
$env:Database__ApplyMigrations = "false"
$env:Demo__SeedData = "true"
$env:Storage__RootPath = $storagePath
$env:Forensics__WorkerDirectory = $workerDir
$env:Forensics__PythonExecutable = "python"
$env:Analysis__RunBackgroundWorker = "true"
$env:Logging__LogLevel__Microsoft__EntityFrameworkCore__Database__Command = "Warning"

$apiArgs = @("run", "--project", "apps/api/Veritas.Api", "--no-launch-profile")
if ($NoBuild) {
    $apiArgs += "--no-build"
}

$api = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList $apiArgs `
    -WorkingDirectory $root `
    -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $logDir "api.out.log") `
    -RedirectStandardError (Join-Path $logDir "api.err.log") `
    -PassThru

Set-Content -LiteralPath (Join-Path $logDir "api.pid") -Value $api.Id

$env:VITE_API_BASE_URL = "http://127.0.0.1:8080/api"
$web = Start-Process `
    -FilePath "npm.cmd" `
    -ArgumentList @("run", "dev", "--", "--host", "127.0.0.1", "--port", "5173") `
    -WorkingDirectory (Join-Path $root "apps/web") `
    -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $logDir "web.out.log") `
    -RedirectStandardError (Join-Path $logDir "web.err.log") `
    -PassThru

Set-Content -LiteralPath (Join-Path $logDir "web.pid") -Value $web.Id

& (Join-Path $PSScriptRoot "wait-health.ps1") `
    -ApiUrl "http://127.0.0.1:8080/api/health" `
    -WebUrl "http://127.0.0.1:5173" `
    -TimeoutSeconds 90

Write-Host "Non-Docker stack is running:"
Write-Host "  API: http://127.0.0.1:8080/api/health"
Write-Host "  Web: http://127.0.0.1:5173"
