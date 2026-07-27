param(
    [string] $Image = "docker.io/library/postgres:17",
    [int] $PostgreSqlPort = 55437,
    [int] $ServerPort = 5177
)

$ErrorActionPreference = "Stop"
$containerName = "royalidentity-server-" + [Guid]::NewGuid().ToString("N")
$databasePassword = [Guid]::NewGuid().ToString("N")
$connection = "Host=127.0.0.1;Port=$PostgreSqlPort;Database=royalidentity;Username=royalidentity;Password=$databasePassword"
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("royalidentity-server-" + [Guid]::NewGuid().ToString("N"))
$keyRingPath = Join-Path $temporaryRoot "keys"
$standardOutputPath = Join-Path $temporaryRoot "server.out.log"
$standardErrorPath = Join-Path $temporaryRoot "server.err.log"
$serverProcess = $null

New-Item -ItemType Directory -Path $keyRingPath -Force | Out-Null

try {
    podman run --name $containerName --rm -d `
        -e "POSTGRES_USER=royalidentity" `
        -e "POSTGRES_PASSWORD=$databasePassword" `
        -e "POSTGRES_DB=royalidentity" `
        -p "${PostgreSqlPort}:5432" `
        $Image | Out-Null

    $databaseReady = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        podman exec $containerName pg_isready -U royalidentity -d royalidentity 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $databaseReady = $true
            break
        }

        Start-Sleep -Seconds 1
    }
    if (-not $databaseReady) {
        throw "PostgreSQL did not become ready."
    }

    $env:RI_SERVER_TEST_CONFIGURATION = $connection
    $env:RI_SERVER_TEST_OPERATIONAL = $connection
    $env:RI_SERVER_TEST_USERS = $connection
    dotnet run --project RoyalIdentity.Migrations --no-build -- `
        --provider postgresql `
        --families all `
        --configuration-connection-env RI_SERVER_TEST_CONFIGURATION `
        --operational-connection-env RI_SERVER_TEST_OPERATIONAL `
        --user-accounts-connection-env RI_SERVER_TEST_USERS `
        --database-topology shared `
        --seed product `
        --server-admin-redirect-uri "http://127.0.0.1:$ServerPort/callback" `
        --key-protector data-protection `
        --data-protection-key-ring $keyRingPath `
        --data-protection-app-name RoyalIdentity.Server
    if ($LASTEXITCODE -ne 0) {
        throw "The migration runner failed."
    }

    $env:RoyalIdentity__Connections__Configuration__ConnectionString = $connection
    $env:RoyalIdentity__Connections__Operational__ConnectionString = $connection
    $env:RoyalIdentity__Connections__UserAccounts__ConnectionString = $connection
    $env:RoyalIdentity__DataProtection__KeyRingPath = $keyRingPath
    $env:RoyalIdentity__DataProtection__ApplicationName = "RoyalIdentity.Server"
    $env:RoyalIdentity__DataProtection__OperationalPayloadProfileId = "default"
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$ServerPort"

    $serverProcess = Start-Process dotnet `
        -ArgumentList @("run", "--project", "RoyalIdentity.Server", "--no-build", "--no-launch-profile") `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $standardOutputPath `
        -RedirectStandardError $standardErrorPath

    $serverReady = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($serverProcess.HasExited) {
            break
        }

        try {
            $response = Invoke-WebRequest `
                -Uri "http://127.0.0.1:$ServerPort/" `
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

    Write-Output "RoyalIdentity.Server PostgreSQL 17 validation passed."
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }

    podman rm -f $containerName 2>$null | Out-Null

    $resolvedRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $isOwnedTemporaryPath =
        $resolvedRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedRoot).StartsWith("royalidentity-server-", [StringComparison]::Ordinal)
    if ($isOwnedTemporaryPath -and (Test-Path $resolvedRoot)) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
