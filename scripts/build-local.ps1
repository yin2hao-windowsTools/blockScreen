[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$OutputRoot = 'output'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [string]$WorkingDirectory
    )

    if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        & $FilePath @Arguments
    }
    else {
        Push-Location -LiteralPath $WorkingDirectory
        try {
            & $FilePath @Arguments
        }
        finally {
            Pop-Location
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
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
            throw "Unsupported runtime '$Runtime'."
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
            throw "Unsupported runtime '$Runtime'."
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

function Assert-PathInsideRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Target
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $targetFullPath = [System.IO.Path]::GetFullPath($Target)

    if (-not $targetFullPath.StartsWith($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside repository root: $targetFullPath"
    }
}

function Stop-OutputRootProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputRoot
    )

    if (-not (Test-Path -LiteralPath $OutputRoot)) {
        return
    }

    $outputRootPrefix = [System.IO.Path]::GetFullPath($OutputRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $currentProcessId = [System.Diagnostics.Process]::GetCurrentProcess().Id
    $runningOutputProcesses = @(Get-CimInstance -ClassName Win32_Process | Where-Object {
        $_.ProcessId -ne $currentProcessId `
            -and -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) `
            -and [System.IO.Path]::GetFullPath($_.ExecutablePath).StartsWith(
                $outputRootPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)
    })

    foreach ($process in $runningOutputProcesses) {
        Write-Host "Stopping running local build process: $($process.Name) (PID $($process.ProcessId))"
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
    }

    foreach ($process in $runningOutputProcesses) {
        try {
            Wait-Process -Id $process.ProcessId -Timeout 10 -ErrorAction Stop
        }
        catch {
            if (Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue) {
                throw "Timed out waiting for local build process $($process.Name) (PID $($process.ProcessId)) to exit."
            }
        }
    }
}

function Invoke-LauncherBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

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

        Invoke-Tool `
            -FilePath 'go' `
            -Arguments @(
                'build',
                '-trimpath',
                '-ldflags',
                "-H=windowsgui -s -w -X main.appVersion=local -X main.requiredArchitecture=$DotNetArchitectureLabel",
                '-o',
                $OutputPath,
                '.'
            ) `
            -WorkingDirectory $ProjectPath
    }
    finally {
        Set-EnvironmentVariableForProcess -Name 'GOOS' -Value $previousGoos
        Set-EnvironmentVariableForProcess -Name 'GOARCH' -Value $previousGoarch
        Set-EnvironmentVariableForProcess -Name 'CGO_ENABLED' -Value $previousCgoEnabled
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'ScreenShade.App\ScreenShade.App.csproj'
$launcherProjectPath = Join-Path $repoRoot 'Launcher'
$goArchitecture = ConvertTo-GoArchitecture -Runtime $Runtime
$dotNetArchitectureLabel = ConvertTo-DotNetArchitectureLabel -Runtime $Runtime

if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
}
else {
    $resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}

Assert-PathInsideRoot -Root $repoRoot -Target $resolvedOutputRoot

Stop-OutputRootProcesses -OutputRoot $resolvedOutputRoot

if (Test-Path -LiteralPath $resolvedOutputRoot) {
    Remove-Item -LiteralPath $resolvedOutputRoot -Recurse -Force
}

$managedAppOutputRoot = Join-Path $resolvedOutputRoot 'app'
New-Item -ItemType Directory -Path $managedAppOutputRoot | Out-Null

Invoke-Tool -FilePath 'dotnet' -Arguments @('restore', $projectPath)
Invoke-Tool -FilePath 'dotnet' -Arguments @(
    'publish',
    $projectPath,
    '--configuration',
    $Configuration,
    '--runtime',
    $Runtime,
    '--self-contained',
    'false',
    '--output',
    $managedAppOutputRoot,
    '/p:AssemblyName=blockScreen.App',
    '/p:PublishSingleFile=false'
)

Invoke-LauncherBuild `
    -ProjectPath $launcherProjectPath `
    -OutputPath (Join-Path $resolvedOutputRoot 'blockScreen.exe') `
    -GoArchitecture $goArchitecture `
    -DotNetArchitectureLabel $dotNetArchitectureLabel

Set-Content -LiteralPath (Join-Path $resolvedOutputRoot 'LOCAL_TEST.txt') -Encoding ASCII -Value @(
    'blockScreen local test build',
    '',
    'Run blockScreen.exe from this folder.',
    'This output is generated by scripts/build-local.ps1 and is not intended for release.'
)

Write-Host "Built local test output:"
Write-Host " - $resolvedOutputRoot"
