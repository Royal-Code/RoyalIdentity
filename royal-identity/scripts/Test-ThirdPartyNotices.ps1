<#
.SYNOPSIS
Validates the IS4/IdentityModel provenance inventory and Apache-2.0 notices.

.DESCRIPTION
Checks the combined AGPL/Apache distribution, validates every inventory path and classification, discovers
basename candidates across the complete bounded source roots, and verifies prominent modification/license
headers on every file classified as derived. A new upstream NOTICE or an unclassified candidate fails closed and
requires a human provenance review.

.EXAMPLE
./scripts/Test-ThirdPartyNotices.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$repositoryRoot = (Resolve-Path (Join-Path $solutionRoot "..")).Path
$inventoryPath = Join-Path $solutionRoot ".ai/analisys/an-oidc-session-management-provenance.json"
$agplPath = Join-Path $repositoryRoot "LICENSE"
$apachePath = Join-Path $repositoryRoot "LICENSES/Apache-2.0.txt"
$upstreamApachePath = Join-Path $repositoryRoot "old-is4/LICENSE"
$noticePath = Join-Path $repositoryRoot "THIRD-PARTY-NOTICES.md"
$readmePath = Join-Path $repositoryRoot "README.md"
$upstreamRoots = @(
    "old-is4/src/IdentityServer4/src",
    "old-is4/src/IdentityModel"
)
$sourceExtensions = @(".cs", ".cshtml", ".razor", ".js")

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool] $Condition,
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RepositoryRelativePath {
    param([Parameter(Mandatory = $true)][string] $Path)

    $absolutePath = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    Assert-True ($absolutePath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) `
        "Path is outside the repository root: $absolutePath"
    return $absolutePath.Substring($rootPrefix.Length).Replace("\", "/")
}

foreach ($requiredPath in @(
    $inventoryPath,
    $agplPath,
    $apachePath,
    $upstreamApachePath,
    $noticePath,
    $readmePath
)) {
    Assert-True (Test-Path -LiteralPath $requiredPath -PathType Leaf) "Required file is missing: $requiredPath"
}

$agpl = Get-Content -Raw -Encoding utf8 -LiteralPath $agplPath
Assert-True ($agpl.IndexOf("GNU AFFERO GENERAL PUBLIC LICENSE", [StringComparison]::Ordinal) -ge 0) `
    "The repository LICENSE is not AGPLv3."

$apacheText = (Get-Content -Raw -Encoding utf8 -LiteralPath $apachePath).Replace("`r`n", "`n").TrimEnd([char[]]"`n")
$upstreamApacheText = (Get-Content -Raw -Encoding utf8 -LiteralPath $upstreamApachePath).Replace("`r`n", "`n").TrimEnd([char[]]"`n")
Assert-True ($apacheText -eq $upstreamApacheText) `
    "LICENSES/Apache-2.0.txt is not a complete textual copy of old-is4/LICENSE."

$notice = Get-Content -Raw -Encoding utf8 -LiteralPath $noticePath
$readme = Get-Content -Raw -Encoding utf8 -LiteralPath $readmePath
foreach ($requiredText in @("IdentityServer4", "IdentityModel", "Apache License 2.0", "provenance")) {
    Assert-True ($notice.IndexOf($requiredText, [StringComparison]::OrdinalIgnoreCase) -ge 0) `
        "THIRD-PARTY-NOTICES.md does not mention '$requiredText'."
}
foreach ($requiredText in @("AGPL", "Apache", "THIRD-PARTY-NOTICES.md")) {
    Assert-True ($readme.IndexOf($requiredText, [StringComparison]::OrdinalIgnoreCase) -ge 0) `
        "README.md does not mention '$requiredText'."
}

$upstreamNotice = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "old-is4") -File -Force |
    Where-Object { $_.Name -match '^NOTICE(?:\..+)?$' }
Assert-True (@($upstreamNotice).Count -eq 0) `
    "The upstream distribution now contains NOTICE; review and transport applicable notices before continuing."

$inventory = Get-Content -Raw -Encoding utf8 -LiteralPath $inventoryPath | ConvertFrom-Json
Assert-True ($inventory.schemaVersion -eq 1) "Unsupported provenance inventory schema."
Assert-True ($inventory.summary.pending -eq 0) "The provenance inventory reports pending candidates."

$entriesByProduction = @{}
foreach ($entry in $inventory.entries) {
    Assert-True ($entry.classification -in @("derived", "independent")) `
        "Candidate '$($entry.production)' has unresolved classification '$($entry.classification)'."
    Assert-True (-not $entriesByProduction.ContainsKey($entry.production)) `
        "Duplicate production path in provenance inventory: $($entry.production)"

    $productionPath = Join-Path $repositoryRoot $entry.production
    Assert-True (Test-Path -LiteralPath $productionPath -PathType Leaf) `
        "Inventory production path does not exist: $($entry.production)"
    Assert-True (@($entry.upstream).Count -gt 0) `
        "Candidate '$($entry.production)' has no upstream evidence path."
    foreach ($upstream in $entry.upstream) {
        Assert-True (Test-Path -LiteralPath (Join-Path $repositoryRoot $upstream) -PathType Leaf) `
            "Inventory upstream path does not exist: $upstream"
    }

    $source = Get-Content -Raw -Encoding utf8 -LiteralPath $productionPath
    $hasDerivedHeader =
        $source.IndexOf("material derived from IdentityServer4 and/or IdentityModel", [StringComparison]::Ordinal) -ge 0 -and
        $source.IndexOf("Licensed under Apache License 2.0", [StringComparison]::Ordinal) -ge 0 -and
        $source.IndexOf("Modified by RoyalIdentity contributors", [StringComparison]::Ordinal) -ge 0
    if ($entry.classification -eq "derived") {
        Assert-True $hasDerivedHeader "Derived file lacks the required prominent notice: $($entry.production)"
        Assert-True ($source.IndexOf("See LICENSE in the project root for license information", [StringComparison]::OrdinalIgnoreCase) -lt 0) `
            "Derived file still points an Apache notice at the AGPL root LICENSE: $($entry.production)"
    }
    else {
        Assert-True (-not $hasDerivedHeader) `
            "Independent file incorrectly carries the standardized Apache-derived header: $($entry.production)"
    }

    $entriesByProduction[$entry.production] = $entry
}

$tracked = & git -C $repositoryRoot ls-files
if ($LASTEXITCODE -ne 0) {
    throw "git ls-files failed while enumerating production sources."
}
$productionFiles = @($tracked |
    Where-Object {
        $_ -match '^royal-identity/RoyalIdentity[^/]*/.*\.(cs|cshtml|razor|js)$' -and
        $_ -notmatch '/(bin|obj|wwwroot/lib)/'
    } |
    ForEach-Object { Join-Path $repositoryRoot $_ } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })

$upstreamFiles = @()
foreach ($root in $upstreamRoots) {
    $absoluteRoot = Join-Path $repositoryRoot $root
    Assert-True (Test-Path -LiteralPath $absoluteRoot -PathType Container) "Missing upstream root: $root"
    $upstreamFiles += Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File |
        Where-Object {
            $_.Extension -in $sourceExtensions -and
            $_.FullName -notmatch '[\\/](bin|obj|wwwroot[\\/]lib)[\\/]'
        } |
        Select-Object -ExpandProperty FullName
}

$upstreamByName = $upstreamFiles |
    Group-Object { [IO.Path]::GetFileName($_) } -AsHashTable -AsString
$unclassified = [Collections.Generic.List[string]]::new()
foreach ($productionPath in $productionFiles) {
    $name = [IO.Path]::GetFileName($productionPath)
    if (-not $upstreamByName.ContainsKey($name)) {
        continue
    }

    $productionRelative = Get-RepositoryRelativePath $productionPath
    if (-not $entriesByProduction.ContainsKey($productionRelative)) {
        $unclassified.Add($productionRelative)
        continue
    }

    $recordedUpstream = @($entriesByProduction[$productionRelative].upstream)
    foreach ($candidate in @($upstreamByName[$name])) {
        $candidateRelative = Get-RepositoryRelativePath $candidate
        Assert-True ($candidateRelative -in $recordedUpstream) `
            "Inventory omits basename candidate '$candidateRelative' for '$productionRelative'."
    }
}

Assert-True ($unclassified.Count -eq 0) `
    "Unclassified provenance candidates: $($unclassified -join ', ')"

$derivedCount = @($inventory.entries | Where-Object classification -eq "derived").Count
$independentCount = @($inventory.entries | Where-Object classification -eq "independent").Count
Write-Output (
    "Third-party notice validation passed: $($productionFiles.Count) production files and " +
    "$($upstreamFiles.Count) upstream files scanned; $derivedCount derived and " +
    "$independentCount independent candidates; no pending classification or upstream NOTICE.")
