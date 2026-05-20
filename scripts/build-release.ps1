[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$OutputRoot = 'artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-ReleaseVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputTag
    )

    if ($InputTag -notmatch '^v(?<core>\d+(?:\.\d+)*)(?<suffix>-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
        throw "Tag '$InputTag' is invalid. Expected v-prefixed numeric version, for example v1, v1.2.3, or v1.2.3-alpha."
    }

    $parts = [System.Collections.Generic.List[string]]::new()
    foreach ($part in $Matches['core'].Split('.')) {
        $parts.Add($part)
    }

    while ($parts.Count -lt 3) {
        $parts.Add('0')
    }

    $numericVersion = ($parts.GetRange(0, 3) -join '.')
    $suffix = if ($Matches.ContainsKey('suffix')) { $Matches['suffix'] } else { '' }
    $isPrerelease = -not [string]::IsNullOrEmpty($suffix)

    [pscustomobject]@{
        NumericVersion = $numericVersion
        PackageVersion = "$numericVersion$suffix"
        IsPrerelease = $isPrerelease
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-PathInsideRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Target
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $targetFullPath = [System.IO.Path]::GetFullPath($Target)

    if (-not $targetFullPath.StartsWith($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside repository root: $targetFullPath"
    }
}

function New-InstallerSource {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$SourceExePath,

        [Parameter(Mandatory = $true)]
        [string]$ProductVersion
    )

    $escapedSourceExePath = [System.Security.SecurityElement]::Escape($SourceExePath)
    $escapedProductVersion = [System.Security.SecurityElement]::Escape($ProductVersion)

    $content = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package
    Name="A1 Screen Shade"
    Manufacturer="A1"
    Version="$escapedProductVersion"
    UpgradeCode="{32AA0A45-07FA-45BB-A8A5-1D569217803C}"
    Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of A1 Screen Shade is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Feature Id="MainFeature" Title="A1 Screen Shade" Level="1">
      <ComponentRef Id="AppExecutable" />
      <ComponentRef Id="StartMenuShortcut" />
      <ComponentRef Id="DesktopShortcut" />
    </Feature>

    <StandardDirectory Id="ProgramFilesFolder">
      <Directory Id="INSTALLFOLDER" Name="A1 Screen Shade">
        <Component Id="AppExecutable" Guid="{8A53D80D-705A-4F06-9757-CF5642415948}">
          <File Id="A1ScreenShadeExe" Source="$escapedSourceExePath" KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="A1 Screen Shade">
        <Component Id="StartMenuShortcut" Guid="{96FCD50B-AE4F-490D-BD14-91644735BDFB}">
          <Shortcut Id="ApplicationStartMenuShortcut" Name="A1 Screen Shade" Target="[INSTALLFOLDER]A1ScreenShade.exe" WorkingDirectory="INSTALLFOLDER" />
          <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall" />
          <RegistryValue Root="HKCU" Key="Software\A1 Screen Shade" Name="Installed" Type="integer" Value="1" KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="DesktopFolder">
      <Component Id="DesktopShortcut" Guid="{C9051A3E-3BB4-4A6B-9D5E-30E5A6E2AA5A}">
        <Shortcut Id="ApplicationDesktopShortcut" Name="A1 Screen Shade" Target="[INSTALLFOLDER]A1ScreenShade.exe" WorkingDirectory="INSTALLFOLDER" />
        <RegistryValue Root="HKCU" Key="Software\A1 Screen Shade" Name="DesktopShortcut" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </StandardDirectory>
  </Package>
</Wix>
"@

    Set-Content -LiteralPath $Path -Value $content -Encoding UTF8
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'ScreenShade.App\ScreenShade.App.csproj'
$version = ConvertTo-ReleaseVersion -InputTag $Tag

if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $artifactsRoot = [System.IO.Path]::GetFullPath($OutputRoot)
}
else {
    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}

Assert-PathInsideRoot -Root $repoRoot -Target $artifactsRoot

if (Test-Path -LiteralPath $artifactsRoot) {
    Remove-Item -LiteralPath $artifactsRoot -Recurse -Force
}

$publishRoot = Join-Path $artifactsRoot 'publish'
$packageRoot = Join-Path $artifactsRoot 'packages'
$wixRoot = Join-Path $artifactsRoot 'wix'
$singleExePublishRoot = Join-Path $publishRoot 'exe'
$portablePublishRoot = Join-Path $publishRoot 'portable'

New-Item -ItemType Directory -Path $singleExePublishRoot, $portablePublishRoot, $packageRoot, $wixRoot | Out-Null

$commonVersionProperties = @(
    '/p:AssemblyName=A1ScreenShade',
    "/p:Version=$($version.PackageVersion)",
    "/p:PackageVersion=$($version.PackageVersion)",
    "/p:AssemblyVersion=$($version.NumericVersion)",
    "/p:FileVersion=$($version.NumericVersion)",
    "/p:InformationalVersion=$($version.PackageVersion)",
    '/p:DebugType=none',
    '/p:DebugSymbols=false'
)

$commonPublishArguments = @(
    'publish',
    $projectPath,
    '--configuration',
    $Configuration,
    '--runtime',
    $Runtime,
    '--self-contained',
    'true'
) + $commonVersionProperties

Invoke-DotNet -Arguments @('restore', $projectPath)
Invoke-DotNet -Arguments @('tool', 'restore')

Invoke-DotNet -Arguments ($commonPublishArguments + @(
    '--output',
    $singleExePublishRoot,
    '/p:PublishSingleFile=true',
    '/p:EnableCompressionInSingleFile=true'
))

$assetPrefix = "A1ScreenShade-$($version.PackageVersion)-$Runtime"
$singleExeSource = Join-Path $singleExePublishRoot 'A1ScreenShade.exe'
$exeAssetPath = Join-Path $packageRoot "$assetPrefix.exe"

if (-not (Test-Path -LiteralPath $singleExeSource)) {
    throw "Expected single EXE was not produced: $singleExeSource"
}

Copy-Item -LiteralPath $singleExeSource -Destination $exeAssetPath -Force

Invoke-DotNet -Arguments ($commonPublishArguments + @(
    '--output',
    $portablePublishRoot,
    '/p:PublishSingleFile=false'
))

$portableReadmePath = Join-Path $portablePublishRoot 'PORTABLE.txt'
Set-Content -LiteralPath $portableReadmePath -Encoding ASCII -Value @(
    'A1 Screen Shade portable package',
    '',
    'Run A1ScreenShade.exe from this folder.',
    'Keep all files together.',
    'This package does not install services, shortcuts, startup items, registry entries, or files outside this directory.'
)

$portableZipPath = Join-Path $packageRoot "$assetPrefix-portable.zip"
Compress-Archive -Path (Join-Path $portablePublishRoot '*') -DestinationPath $portableZipPath -Force

$installerSourcePath = Join-Path $wixRoot 'A1ScreenShade.wxs'
$msiAssetPath = Join-Path $packageRoot "$assetPrefix.msi"
New-InstallerSource -Path $installerSourcePath -SourceExePath $singleExeSource -ProductVersion $version.NumericVersion
Invoke-DotNet -Arguments @('tool', 'run', 'wix', '--', 'build', $installerSourcePath, '-out', $msiAssetPath, '-pdbtype', 'none')

$metadataPath = Join-Path $artifactsRoot 'release-metadata.json'
[pscustomobject]@{
    tag = $Tag
    numericVersion = $version.NumericVersion
    packageVersion = $version.PackageVersion
    isPrerelease = $version.IsPrerelease
    assets = [pscustomobject]@{
        exe = $exeAssetPath
        msi = $msiAssetPath
        portable = $portableZipPath
    }
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding UTF8

Write-Host "Built release assets for $Tag as $($version.PackageVersion)."
Get-ChildItem -LiteralPath $packageRoot -File | ForEach-Object {
    Write-Host " - $($_.Name)"
}
