<#
.SYNOPSIS
Runs the opt-in cross-implementation probe for the Check Session iframe JavaScript.

.DESCRIPTION
Builds a small .NET emitter outside RoyalIdentity.sln. The emitter invokes the compiled C#
SessionStateFormat and CheckSessionResult, then sends their envelope and rendered HTML to Node. Node executes
the actual inline script with its Web Crypto implementation and verifies unchanged/changed/error, malformed
input, origin binding, client mismatch and the event.source boundary.

Node is deliberately opt-in and is not required by dotnet test RoyalIdentity.sln. The real Chromium acceptance
remains owned by Fase 6 of plan-oidc-session-management.md.

.EXAMPLE
./scripts/Test-CheckSessionJavaScript.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js was not found in PATH. Install Node.js 20 or newer to run this opt-in probe."
}

$nodeVersion = (& node --version).TrimStart("v")
$nodeMajor = [int]($nodeVersion.Split('.')[0])
if ($nodeMajor -lt 20) {
    throw "Node.js 20 or newer is required; found $nodeVersion."
}

$probeRoot = Join-Path $PSScriptRoot "CheckSessionJavaScriptProbe"
$project = Join-Path $probeRoot "CheckSessionJavaScriptProbe.csproj"
$javascript = Join-Path $probeRoot "probe.mjs"

& dotnet build $project --configuration Release --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw "The Check Session JavaScript probe emitter did not build."
}

$payload = & dotnet run --project $project --configuration Release --no-build --no-launch-profile
if ($LASTEXITCODE -ne 0) {
    throw "The Check Session JavaScript probe emitter failed."
}

$payload | & node $javascript
if ($LASTEXITCODE -ne 0) {
    throw "The Check Session JavaScript probe failed."
}
