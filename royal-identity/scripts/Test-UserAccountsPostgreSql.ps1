<#
.SYNOPSIS
Runs the UserAccounts PostgreSQL migration tests against an ephemeral Podman container.

.DESCRIPTION
Starts the selected Podman machine when necessary, publishes PostgreSQL on a dynamically allocated non-default host
port, waits for pg_isready, supplies the connection string only to the test process, and removes the container in a
finally block. The machine is deliberately left running because it may be shared by other containers.

.EXAMPLE
./scripts/Test-UserAccountsPostgreSql.ps1

.EXAMPLE
./scripts/Test-UserAccountsPostgreSql.ps1 -Image docker.io/library/postgres:18-alpine -KeepContainer
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

$connectionVariable = "ROYALIDENTITY_TEST_POSTGRES"
$containerName = "royalidentity-useraccounts-pg-$PID-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$containerCreated = $false
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$previousConnection = [Environment]::GetEnvironmentVariable($connectionVariable, "Process")

function Invoke-Podman {
	param(
		[Parameter(Mandatory = $true)]
		[string[]] $Arguments
	)

	# Windows PowerShell turns native stderr lines into ErrorRecord objects when stderr is redirected. Podman writes
	# normal progress (for example, image-pull status) to stderr, so temporarily keep those records non-terminating
	# and decide success exclusively from the native process exit code.
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

$database = "royalidentity_tests"
$username = "royalidentity_tests"
$password = [Guid]::NewGuid().ToString("N")

try {
	Write-Host "Starting ephemeral PostgreSQL container '$containerName'..."
	Invoke-Podman -Arguments @(
		"run", "--detach", "--rm",
		"--name", $containerName,
		"--publish", "127.0.0.1::5432",
		"--env", "POSTGRES_DB=$database",
		"--env", "POSTGRES_USER=$username",
		"--env", "POSTGRES_PASSWORD=$password",
		"--health-cmd", "pg_isready -U $username -d $database",
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

		if ($health -eq "healthy") {
			break
		}

		if ($health -eq "unhealthy") {
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

	$hostPort = [int] $Matches.port
	if ($hostPort -eq 5432) {
		throw "Podman unexpectedly selected the default PostgreSQL host port 5432."
	}

	$connectionString = "Host=127.0.0.1;Port=$hostPort;Database=$database;Username=$username;Password=$password;Pooling=false;Include Error Detail=true"
	[Environment]::SetEnvironmentVariable($connectionVariable, $connectionString, "Process")

	Write-Host "PostgreSQL is healthy on dynamic host port $hostPort. Running provider migration tests..."
	Push-Location $repositoryRoot
	try {
		& dotnet test "Tests.UserAccounts/Tests.UserAccounts.csproj" --filter "Category=PostgreSql"
		if ($LASTEXITCODE -ne 0) {
			$testExitCode = $LASTEXITCODE
			$logs = Invoke-Podman -Arguments @("logs", $containerName)
			throw "PostgreSQL migration tests failed with exit code $testExitCode. Container logs:`n$($logs -join [Environment]::NewLine)"
		}
	}
	finally {
		Pop-Location
	}
}
finally {
	[Environment]::SetEnvironmentVariable($connectionVariable, $previousConnection, "Process")

	if ($containerCreated -and -not $KeepContainer) {
		& podman rm --force $containerName 2>&1 | Out-Null
	}
	elseif ($containerCreated) {
		Write-Host "Container '$containerName' was kept for inspection."
	}
}
