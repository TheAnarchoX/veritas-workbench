param(
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

$tempBase = Join-Path ([System.IO.Path]::GetTempPath()) ("veritas-docker-preflight-" + [Guid]::NewGuid().ToString("N"))
$stdout = "$tempBase.out"
$stderr = "$tempBase.err"

try {
    $process = Start-Process `
        -FilePath "docker" `
        -ArgumentList @("version", "--format", "{{.Server.Version}}") `
        -NoNewWindow `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru

    $completed = $process.WaitForExit($TimeoutSeconds * 1000)
    if (-not $completed) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "Docker did not respond within $TimeoutSeconds seconds. Start or repair Docker Desktop, then retry."
    }

    $out = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { "" }
    $err = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { "" }
    $combined = "$out`n$err".Trim()

    if ($process.ExitCode -ne 0 -or $combined -match "failed to connect|Internal Server Error|error during connect") {
        throw "Docker is installed but the daemon is not healthy. Output: $combined"
    }

    Write-Host "Docker daemon is responsive: $($out.Trim())"
}
finally {
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
}
