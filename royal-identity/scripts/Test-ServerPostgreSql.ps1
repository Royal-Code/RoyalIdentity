<#
.SYNOPSIS
Validates the production Server composition against an ephemeral PostgreSQL 17 container.

.DESCRIPTION
Starts PostgreSQL on a dynamically allocated non-default host port, provisions all three storage families with
the external runner and Product seed, starts RoyalIdentity.Server on a dynamically allocated port, and exercises
OIDC discovery plus an authorization-code request through the interactive login challenge. The container, Server
process and temporary Data Protection key ring are removed in finally.

The Product seed deliberately creates no account. A successful redirect to the login UI is therefore the complete
bounded acceptance for this script; account creation belongs to administration, not Server startup or migrations.

.EXAMPLE
./scripts/Test-ServerPostgreSql.ps1
#>
[CmdletBinding()]
param(
    [string] $MachineName = "podman-machine-default",
    [string] $Image = "docker.io/library/postgres:17-alpine",
    [int] $StartupTimeoutSeconds = 90,
    [switch] $KeepContainer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$containerName = "royalidentity-server-pg-$PID-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$databasePassword = [Guid]::NewGuid().ToString("N")
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("royalidentity-server-" + [Guid]::NewGuid().ToString("N"))
$keyRingPath = Join-Path $temporaryRoot "keys"
$standardOutputPath = Join-Path $temporaryRoot "server.out.log"
$standardErrorPath = Join-Path $temporaryRoot "server.err.log"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$serverProcess = $null
$containerCreated = $false
$environmentNames = @(
    "RI_SERVER_TEST_CONFIGURATION",
    "RI_SERVER_TEST_OPERATIONAL",
    "RI_SERVER_TEST_USERS",
    "RoyalIdentity__Connections__Configuration__ConnectionString",
    "RoyalIdentity__Connections__Operational__ConnectionString",
    "RoyalIdentity__Connections__UserAccounts__ConnectionString",
    "RoyalIdentity__DataProtection__KeyRingPath",
    "RoyalIdentity__DataProtection__ApplicationName",
    "RoyalIdentity__DataProtection__OperationalPayloadProfileId",
    "ASPNETCORE_URLS"
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

function Invoke-Podman {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & podman @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "podman $($Arguments[0]) failed: $($output -join [Environment]::NewLine)"
    }

    return $output
}

function Get-DynamicTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([Net.IPEndPoint] $listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

if (-not (Get-Command podman -ErrorAction SilentlyContinue)) {
    throw "Podman was not found in PATH. Install Podman before running this script."
}

$previousErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = "Continue"
    $machineState = & podman machine inspect $MachineName --format "{{.State}}" 2>&1
    $machineInspectExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

if ($machineInspectExitCode -ne 0) {
    throw "Podman machine '$MachineName' does not exist. Create it with 'podman machine init $MachineName'."
}

if (($machineState | Select-Object -First 1).Trim() -ne "running") {
    Write-Host "Starting Podman machine '$MachineName'..."
    Invoke-Podman -Arguments @("machine", "start", $MachineName) | Out-Host
}

Invoke-Podman -Arguments @("info", "--format", "{{.Host.OS}}") | Out-Null

New-Item -ItemType Directory -Path $keyRingPath -Force | Out-Null

try {
    Write-Host "Starting ephemeral PostgreSQL container '$containerName'..."
    Invoke-Podman -Arguments @(
        "run", "--detach", "--rm",
        "--name", $containerName,
        "--publish", "127.0.0.1::5432",
        "--env", "POSTGRES_USER=royalidentity",
        "--env", "POSTGRES_PASSWORD=$databasePassword",
        "--env", "POSTGRES_DB=royalidentity",
        "--health-cmd", "pg_isready -U royalidentity -d royalidentity",
        "--health-interval", "1s",
        "--health-timeout", "3s",
        "--health-retries", "$StartupTimeoutSeconds",
        $Image
    ) | Out-Null
    $containerCreated = $true

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $health = "starting"
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $health = (Invoke-Podman -Arguments @(
            "inspect", "--format", "{{.State.Health.Status}}", $containerName
        ) | Select-Object -First 1).Trim()

        if ($health -in @("healthy", "unhealthy")) {
            break
        }

        Start-Sleep -Seconds 1
    }

    if ($health -ne "healthy") {
        $logs = Invoke-Podman -Arguments @("logs", $containerName)
        throw "PostgreSQL did not become healthy (status: $health). Logs:`n$($logs -join [Environment]::NewLine)"
    }

    $portMapping = (Invoke-Podman -Arguments @("port", $containerName, "5432/tcp") | Select-Object -First 1).Trim()
    if ($portMapping -notmatch ":(?<port>\d+)$") {
        throw "Could not determine the PostgreSQL host port from '$portMapping'."
    }

    $postgreSqlPort = [int] $Matches.port
    if ($postgreSqlPort -eq 5432) {
        throw "Podman unexpectedly selected the default PostgreSQL host port 5432."
    }

    $serverPort = Get-DynamicTcpPort
    $serverBaseAddress = "http://127.0.0.1:$serverPort"
    $redirectUri = "$serverBaseAddress/callback"
    $connection = "Host=127.0.0.1;Port=$postgreSqlPort;Database=royalidentity;Username=royalidentity;Password=$databasePassword;Pooling=false"

    [Environment]::SetEnvironmentVariable("RI_SERVER_TEST_CONFIGURATION", $connection, "Process")
    [Environment]::SetEnvironmentVariable("RI_SERVER_TEST_OPERATIONAL", $connection, "Process")
    [Environment]::SetEnvironmentVariable("RI_SERVER_TEST_USERS", $connection, "Process")

    Write-Host "Provisioning all storage families on PostgreSQL host port $postgreSqlPort..."
    Push-Location $repositoryRoot
    try {
        & dotnet run --project RoyalIdentity.Migrations -- `
            --provider postgresql `
            --families all `
            --configuration-connection-env RI_SERVER_TEST_CONFIGURATION `
            --operational-connection-env RI_SERVER_TEST_OPERATIONAL `
            --user-accounts-connection-env RI_SERVER_TEST_USERS `
            --database-topology shared `
            --seed product `
            --server-admin-redirect-uri $redirectUri `
            --key-protector data-protection `
            --data-protection-key-ring $keyRingPath `
            --data-protection-app-name RoyalIdentity.Server
        if ($LASTEXITCODE -ne 0) {
            throw "The migration runner failed with exit code $LASTEXITCODE."
        }

        [Environment]::SetEnvironmentVariable(
            "RoyalIdentity__Connections__Configuration__ConnectionString", $connection, "Process")
        [Environment]::SetEnvironmentVariable(
            "RoyalIdentity__Connections__Operational__ConnectionString", $connection, "Process")
        [Environment]::SetEnvironmentVariable(
            "RoyalIdentity__Connections__UserAccounts__ConnectionString", $connection, "Process")
        [Environment]::SetEnvironmentVariable(
            "RoyalIdentity__DataProtection__KeyRingPath", $keyRingPath, "Process")
        [Environment]::SetEnvironmentVariable(
            "RoyalIdentity__DataProtection__ApplicationName", "RoyalIdentity.Server", "Process")
        [Environment]::SetEnvironmentVariable(
            "RoyalIdentity__DataProtection__OperationalPayloadProfileId", "default", "Process")
        [Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", $serverBaseAddress, "Process")

        $serverProcess = Start-Process dotnet `
            -ArgumentList @("run", "--project", "RoyalIdentity.Server", "--no-launch-profile") `
            -PassThru `
            -WindowStyle Hidden `
            -RedirectStandardOutput $standardOutputPath `
            -RedirectStandardError $standardErrorPath

        $serverReady = $false
        for ($attempt = 0; $attempt -lt $StartupTimeoutSeconds; $attempt++) {
            if ($serverProcess.HasExited) {
                break
            }

            try {
                $response = Invoke-WebRequest `
                    -Uri "$serverBaseAddress/" `
                    -UseBasicParsing `
                    -TimeoutSec 2
                if ($response.StatusCode -eq 200) {
                    $serverReady = $true
                    break
                }
            }
            catch {
                # Startup is asynchronous; retry until the bounded deadline.
            }

            Start-Sleep -Seconds 1
        }

        if (-not $serverReady) {
            if (Test-Path $standardOutputPath) {
                Get-Content $standardOutputPath | Select-Object -Last 50
            }
            if (Test-Path $standardErrorPath) {
                Get-Content $standardErrorPath | Select-Object -Last 50
            }
            throw "RoyalIdentity.Server did not start over the provisioned PostgreSQL database."
        }

        $discovery = Invoke-RestMethod `
            -Uri "$serverBaseAddress/server/.well-known/openid-configuration" `
            -TimeoutSec 10
        if ([string]::IsNullOrWhiteSpace($discovery.authorization_endpoint)) {
            throw "OIDC discovery did not return an authorization endpoint."
        }
        $expectedUiLocales = @("en", "pt-BR", "es-419")
        $actualUiLocales = @($discovery.ui_locales_supported)
        if (($actualUiLocales -join "|") -ne ($expectedUiLocales -join "|")) {
            throw (
                "OIDC discovery did not preserve the PostgreSQL-backed realm locale policy. " +
                "Expected '$($expectedUiLocales -join ',')', got '$($actualUiLocales -join ',')'.")
        }
        if ($discovery.PSObject.Properties.Name -contains "claims_locales_supported") {
            throw "OIDC discovery announced localized claims that the product does not implement."
        }

        $verifierBytes = [byte[]]::new(32)
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $random.GetBytes($verifierBytes)
        }
        finally {
            $random.Dispose()
        }
        $codeVerifier = ([Convert]::ToBase64String($verifierBytes)).TrimEnd("=").Replace("+", "-").Replace("/", "_")
        $challengeBytes = [Text.Encoding]::ASCII.GetBytes($codeVerifier)
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            $challengeHash = $sha256.ComputeHash($challengeBytes)
        }
        finally {
            $sha256.Dispose()
        }
        $codeChallenge = ([Convert]::ToBase64String($challengeHash)).TrimEnd("=").Replace("+", "-").Replace("/", "_")
        $authorizeQuery = [System.Web.HttpUtility]::ParseQueryString([string]::Empty)
        $authorizeQuery["client_id"] = "server_admin"
        $authorizeQuery["response_type"] = "code"
        $authorizeQuery["response_mode"] = "query"
        $authorizeQuery["scope"] = "openid profile"
        $authorizeQuery["redirect_uri"] = $redirectUri
        $authorizeQuery["state"] = "postgresql-acceptance"
        $authorizeQuery["code_challenge"] = $codeChallenge
        $authorizeQuery["code_challenge_method"] = "S256"

        $authorizeResponse = Invoke-WebRequest `
            -Uri "$($discovery.authorization_endpoint)?$($authorizeQuery.ToString())" `
            -UseBasicParsing `
            -TimeoutSec 10
        if ($authorizeResponse.StatusCode -ne 200 -or $authorizeResponse.Content -notmatch "<form") {
            throw "The authorization-code request did not reach the interactive login challenge."
        }

        Write-Output (
            "RoyalIdentity.Server PostgreSQL 17 validation passed " +
            "(dynamic PostgreSQL port $postgreSqlPort, dynamic Server port $serverPort, " +
            "three families, Product seed, OIDC discovery and authorization challenge).")
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }

    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], "Process")
    }

    if ($containerCreated -and -not $KeepContainer) {
        & podman rm --force $containerName 2>&1 | Out-Null
    }
    elseif ($containerCreated) {
        Write-Host "Container '$containerName' was kept for inspection."
    }

    $resolvedRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $isOwnedTemporaryPath =
        $resolvedRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedRoot).StartsWith("royalidentity-server-", [StringComparison]::Ordinal)
    if ($isOwnedTemporaryPath -and (Test-Path $resolvedRoot)) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
