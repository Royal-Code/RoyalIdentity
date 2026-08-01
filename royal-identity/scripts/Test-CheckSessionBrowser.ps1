param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\Tests.Browser\Tests.Browser.csproj'

dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw 'The check-session browser project did not build.'
}

$installer = Join-Path $PSScriptRoot "..\Tests.Browser\bin\$Configuration\net10.0\playwright.ps1"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Playwright installer not found at '$installer'."
}

& $installer install chromium
if ($LASTEXITCODE -ne 0) {
    throw 'Chromium installation for the check-session acceptance failed.'
}

dotnet test $project -c $Configuration --no-build --filter 'FullyQualifiedName~CheckSessionBrowserTests'
if ($LASTEXITCODE -ne 0) {
    throw 'The check-session browser acceptance failed.'
}
