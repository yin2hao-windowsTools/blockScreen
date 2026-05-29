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

function Invoke-Go {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & go @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "go $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function ConvertTo-GoArchitecture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Runtime
    )

    switch ($Runtime) {
        'win-x64' { return 'amd64' }
        'win-x86' { return '386' }
        'win-arm64' { return 'arm64' }
        default {
            throw "Unsupported runtime '$Runtime'. Expected win-x64, win-x86, or win-arm64."
        }
    }
}

function ConvertTo-DotNetArchitectureLabel {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Runtime
    )

    switch ($Runtime) {
        'win-x64' { return 'x64' }
        'win-x86' { return 'x86' }
        'win-arm64' { return 'arm64' }
        default {
            throw "Unsupported runtime '$Runtime'. Expected win-x64, win-x86, or win-arm64."
        }
    }
}

function Set-EnvironmentVariableForProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [string]$Value
    )

    if ($null -eq $Value) {
        Remove-Item -LiteralPath "Env:$Name" -ErrorAction SilentlyContinue
        return
    }

    Set-Item -LiteralPath "Env:$Name" -Value $Value
}

function Invoke-LauncherBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$PackageVersion,

        [Parameter(Mandatory = $true)]
        [string]$GoArchitecture,

        [Parameter(Mandatory = $true)]
        [string]$DotNetArchitectureLabel
    )

    $previousGoos = $env:GOOS
    $previousGoarch = $env:GOARCH
    $previousCgoEnabled = $env:CGO_ENABLED

    try {
        $env:GOOS = 'windows'
        $env:GOARCH = $GoArchitecture
        $env:CGO_ENABLED = '0'

        Invoke-Go -Arguments @(
            'build',
            '-trimpath',
            '-ldflags',
            "-H=windowsgui -s -w -X main.appVersion=$PackageVersion -X main.requiredArchitecture=$DotNetArchitectureLabel",
            '-o',
            $OutputPath,
            $SourcePath
        )
    }
    finally {
        Set-EnvironmentVariableForProcess -Name 'GOOS' -Value $previousGoos
        Set-EnvironmentVariableForProcess -Name 'GOARCH' -Value $previousGoarch
        Set-EnvironmentVariableForProcess -Name 'CGO_ENABLED' -Value $previousCgoEnabled
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
        [string]$LayoutRoot,

        [Parameter(Mandatory = $true)]
        [string]$ProductVersion
    )

    $launcherPath = Join-Path $LayoutRoot 'blockScreen.exe'
    $payloadRoot = Join-Path $LayoutRoot 'app'
    $payloadFiles = @(Get-ChildItem -LiteralPath $payloadRoot -File | Sort-Object Name)

    if (-not (Test-Path -LiteralPath $launcherPath)) {
        throw "Launcher executable was not found: $launcherPath"
    }

    if ($payloadFiles.Count -eq 0) {
        throw "Published application payload is empty: $payloadRoot"
    }

    $escapedLauncherPath = [System.Security.SecurityElement]::Escape($launcherPath)
    $escapedProductVersion = [System.Security.SecurityElement]::Escape($ProductVersion)
    $payloadComponentRefs = [System.Text.StringBuilder]::new()
    $payloadComponents = [System.Text.StringBuilder]::new()

    for ($i = 0; $i -lt $payloadFiles.Count; $i++) {
        $file = $payloadFiles[$i]
        $componentId = "AppPayloadFile$i"
        $fileId = "AppPayloadFileSource$i"
        $escapedSource = [System.Security.SecurityElement]::Escape($file.FullName)
        [void]$payloadComponentRefs.AppendLine("      <ComponentRef Id=""$componentId"" />")
        [void]$payloadComponents.AppendLine("          <Component Id=""$componentId"" Guid=""*"">")
        [void]$payloadComponents.AppendLine("            <File Id=""$fileId"" Source=""$escapedSource"" KeyPath=""yes"" />")
        [void]$payloadComponents.AppendLine("          </Component>")
    }

    $payloadComponentRefXml = $payloadComponentRefs.ToString().TrimEnd()
    $payloadComponentXml = $payloadComponents.ToString().TrimEnd()

    $content = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package
    Name="blockScreen"
    Manufacturer="yin2hao-windowsTools"
    Version="$escapedProductVersion"
    UpgradeCode="{32AA0A45-07FA-45BB-A8A5-1D569217803C}"
    Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of blockScreen is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Feature Id="MainFeature" Title="blockScreen" Level="1">
      <ComponentRef Id="AppExecutable" />
$payloadComponentRefXml
      <ComponentRef Id="StartMenuShortcut" />
      <ComponentRef Id="DesktopShortcut" />
    </Feature>

    <StandardDirectory Id="ProgramFilesFolder">
      <Directory Id="INSTALLFOLDER" Name="blockScreen">
        <Component Id="AppExecutable" Guid="{8A53D80D-705A-4F06-9757-CF5642415948}">
          <File Id="blockScreenExe" Source="$escapedLauncherPath" KeyPath="yes" />
        </Component>
        <Directory Id="AppPayloadFolder" Name="app">
$payloadComponentXml
        </Directory>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="blockScreen">
        <Component Id="StartMenuShortcut" Guid="{96FCD50B-AE4F-490D-BD14-91644735BDFB}">
          <Shortcut Id="ApplicationStartMenuShortcut" Name="blockScreen" Target="[INSTALLFOLDER]blockScreen.exe" WorkingDirectory="INSTALLFOLDER" />
          <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall" />
          <RegistryValue Root="HKCU" Key="Software\blockScreen" Name="Installed" Type="integer" Value="1" KeyPath="yes" />
        </Component>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="DesktopFolder">
      <Component Id="DesktopShortcut" Guid="{C9051A3E-3BB4-4A6B-9D5E-30E5A6E2AA5A}">
        <Shortcut Id="ApplicationDesktopShortcut" Name="blockScreen" Target="[INSTALLFOLDER]blockScreen.exe" WorkingDirectory="INSTALLFOLDER" />
        <RegistryValue Root="HKCU" Key="Software\blockScreen" Name="DesktopShortcut" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </StandardDirectory>
  </Package>
</Wix>
"@

    Set-Content -LiteralPath $Path -Value $content -Encoding UTF8
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'ScreenShade.App\ScreenShade.App.csproj'
$launcherSourcePath = Join-Path $repoRoot 'Launcher\blockscreen_launcher.go'
$version = ConvertTo-ReleaseVersion -InputTag $Tag
$goArchitecture = ConvertTo-GoArchitecture -Runtime $Runtime
$dotNetArchitectureLabel = ConvertTo-DotNetArchitectureLabel -Runtime $Runtime

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
$portablePublishRoot = Join-Path $publishRoot 'portable'
$managedAppPublishRoot = Join-Path $portablePublishRoot 'app'

New-Item -ItemType Directory -Path $portablePublishRoot, $managedAppPublishRoot, $packageRoot, $wixRoot | Out-Null

$commonVersionProperties = @(
    '/p:AssemblyName=blockScreen.App',
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
    'false'
) + $commonVersionProperties

Invoke-DotNet -Arguments @('restore', $projectPath)
Invoke-DotNet -Arguments @('tool', 'restore')

Invoke-DotNet -Arguments ($commonPublishArguments + @(
    '--output',
    $managedAppPublishRoot,
    '/p:PublishSingleFile=false'
))

$assetPrefix = "blockScreen-$($version.PackageVersion)-$Runtime"
$launcherOutputPath = Join-Path $portablePublishRoot 'blockScreen.exe'
Invoke-LauncherBuild `
    -SourcePath $launcherSourcePath `
    -OutputPath $launcherOutputPath `
    -PackageVersion $version.PackageVersion `
    -GoArchitecture $goArchitecture `
    -DotNetArchitectureLabel $dotNetArchitectureLabel

$portableReadmePath = Join-Path $portablePublishRoot 'PORTABLE.txt'
Set-Content -LiteralPath $portableReadmePath -Encoding ASCII -Value @(
    'blockScreen portable package',
    '',
    'Run blockScreen.exe from this folder.',
    'Keep blockScreen.exe and the app folder together.',
    'Requires Microsoft .NET 8 Desktop Runtime. The launcher will open the official download page if it is missing.',
    'This package does not install services, shortcuts, startup items, registry entries, or files outside this directory.'
)

$portableZipPath = Join-Path $packageRoot "$assetPrefix-portable.zip"
Compress-Archive -Path (Join-Path $portablePublishRoot '*') -DestinationPath $portableZipPath -Force

$installerSourcePath = Join-Path $wixRoot 'blockScreen.wxs'
$msiAssetPath = Join-Path $packageRoot "$assetPrefix.msi"
New-InstallerSource -Path $installerSourcePath -LayoutRoot $portablePublishRoot -ProductVersion $version.NumericVersion
Invoke-DotNet -Arguments @('tool', 'run', 'wix', '--', 'build', $installerSourcePath, '-out', $msiAssetPath, '-pdbtype', 'none')

$metadataPath = Join-Path $artifactsRoot 'release-metadata.json'
[pscustomobject]@{
    tag = $Tag
    numericVersion = $version.NumericVersion
    packageVersion = $version.PackageVersion
    isPrerelease = $version.IsPrerelease
    assets = [pscustomobject]@{
        msi = $msiAssetPath
        portable = $portableZipPath
    }
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding UTF8

Write-Host "Built release assets for $Tag as $($version.PackageVersion)."
Get-ChildItem -LiteralPath $packageRoot -File | ForEach-Object {
    Write-Host " - $($_.Name)"
}
