param(
    [Parameter(Mandatory = $true)]
    [string]$ApiUrl,

    [Parameter(Mandatory = $true)]
    [string]$WebUrl,

    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$lastError = $null

while ((Get-Date) -lt $deadline) {
    try {
        $api = Invoke-WebRequest -Uri $ApiUrl -UseBasicParsing -TimeoutSec 5
        $web = Invoke-WebRequest -Uri $WebUrl -UseBasicParsing -TimeoutSec 5
        if ($api.StatusCode -ge 200 -and $api.StatusCode -lt 300 -and $web.StatusCode -ge 200 -and $web.StatusCode -lt 300) {
            Write-Host "Health check passed: API $($api.StatusCode), Web $($web.StatusCode)"
            exit 0
        }
    }
    catch {
        $lastError = $_.Exception.Message
    }

    Start-Sleep -Seconds 2
}

throw "Health check failed after $TimeoutSeconds seconds. Last error: $lastError"
