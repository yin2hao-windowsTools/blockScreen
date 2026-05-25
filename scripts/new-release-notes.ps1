[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return $output
}

function Get-PreviousTag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CurrentTag
    )

    $previousTag = Invoke-Git -Arguments @('describe', '--tags', '--abbrev=0', "$CurrentTag^")
    if ($previousTag) {
        return ($previousTag | Select-Object -First 1)
    }

    return $null
}

function Get-ChangeGroups {
    param(
        [string]$PreviousTag,

        [Parameter(Mandatory = $true)]
        [string]$CurrentTag
    )

    $range = if ([string]::IsNullOrWhiteSpace($PreviousTag)) { $CurrentTag } else { "$PreviousTag..$CurrentTag" }
    $lines = Invoke-Git -Arguments @('log', '--reverse', '--format=%H%x09%s', $range)
    $groups = [ordered]@{
        fix = [System.Collections.Generic.List[object]]::new()
        feature = [System.Collections.Generic.List[object]]::new()
        optimize = [System.Collections.Generic.List[object]]::new()
        ci = [System.Collections.Generic.List[object]]::new()
        enhance = [System.Collections.Generic.List[object]]::new()
        other = [System.Collections.Generic.List[object]]::new()
    }

    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $parts = $line -split "`t", 2
        if ($parts.Count -lt 2) {
            continue
        }

        $hash = $parts[0]
        $subject = $parts[1]
        $type = 'other'
        $message = $subject

        if ($subject -match '^\[(?<type>[^\]]+)\]\s*(?<message>.*)$') {
            $type = $Matches['type'].ToLowerInvariant()
            $message = $Matches['message']
        }

        if (-not $groups.Contains($type)) {
            $type = 'other'
        }

        $groups[$type].Add([pscustomobject]@{
            Hash = $hash
            ShortHash = $hash.Substring(0, 7)
            Message = $message
        })
    }

    return $groups
}

function Get-ReleaseAssetRows {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$CurrentTag,

        [Parameter(Mandatory = $true)]
        [string]$Repository
    )

    $files = Get-ChildItem -LiteralPath $Root -File |
        Where-Object { $_.Extension -in '.exe', '.msi', '.zip' } |
        Sort-Object @{
            Expression = {
                if ($_.Extension -eq '.exe') { 0 }
                elseif ($_.Extension -eq '.msi') { 1 }
                else { 2 }
            }
        }, Name

    foreach ($file in $files) {
        $type = switch -Regex ($file.Name) {
            '\.exe$' { 'EXE installer'; break }
            '\.msi$' { 'MSI installer'; break }
            'portable\.zip$' { 'portable ZIP'; break }
            default { 'package' }
        }

        $downloadUrl = "https://github.com/$Repository/releases/download/$CurrentTag/$($file.Name)"
        [pscustomobject]@{
            Platform = 'Windows'
            Type = $type
            File = $file.Name
            DownloadUrl = $downloadUrl
        }
    }
}

$packageRootFullPath = [System.IO.Path]::GetFullPath($PackageRoot)
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFullPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$previousTag = Get-PreviousTag -CurrentTag $Tag
$changeGroups = Get-ChangeGroups -PreviousTag $previousTag -CurrentTag $Tag
$assetRows = @(Get-ReleaseAssetRows -Root $packageRootFullPath -CurrentTag $Tag -Repository $Repository)

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("## What's Changed")
$lines.Add('')

foreach ($type in $changeGroups.Keys) {
    $changes = @($changeGroups[$type])
    if ($changes.Count -eq 0) {
        continue
    }

    $lines.Add("[$type]:")
    $lines.Add('')

    foreach ($change in $changes) {
        $commitUrl = "https://github.com/$Repository/commit/$($change.Hash)"
        $lines.Add("* [$($change.ShortHash)]($commitUrl) $($change.Message)")
    }

    $lines.Add('')
}

if ($previousTag) {
    $lines.Add("Full Changelog: https://github.com/$Repository/compare/$previousTag...$Tag")
}
else {
    $lines.Add("Full Changelog: https://github.com/$Repository/commits/$Tag")
}

$lines.Add('')
$lines.Add('## 发行版')
$lines.Add('')
$lines.Add('| 平台 | 类型 | 文件 | 快速链接 |')
$lines.Add('| --- | --- | --- | --- |')

foreach ($row in $assetRows) {
    $lines.Add("| $($row.Platform) | $($row.Type) | $($row.File) | [下载]($($row.DownloadUrl)) |")
}

$lines.Add('')

Set-Content -LiteralPath $outputFullPath -Value $lines -Encoding UTF8
Write-Host "Generated release notes: $outputFullPath"
