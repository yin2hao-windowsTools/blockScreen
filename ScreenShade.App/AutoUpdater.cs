using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace ScreenShade.App;

internal static class AutoUpdater
{
    private static readonly HttpClient Client = CreateClient();

    public static AutoUpdatePackage? SelectPackage(UpdateCheckResult result)
    {
        var exeAsset = FindAsset(result, ".exe");
        var msiAsset = FindAsset(result, ".msi");
        var zipAsset = FindAsset(result, ".zip");

        if (IsLikelyInstalledWithMsi())
        {
            return msiAsset is null
                ? ToPackage(exeAsset, AutoUpdatePackageKind.Exe)
                : ToPackage(msiAsset, AutoUpdatePackageKind.Msi);
        }

        if (zipAsset is not null)
        {
            return ToPackage(zipAsset, AutoUpdatePackageKind.Zip);
        }

        return exeAsset is not null
            ? ToPackage(exeAsset, AutoUpdatePackageKind.Exe)
            : ToPackage(msiAsset, AutoUpdatePackageKind.Msi);
    }

    public static async Task DownloadAndApplyAsync(
        AutoUpdatePackage package,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var workingDirectory = CreateWorkingDirectory(package);
        var packagePath = await DownloadPackageAsync(package.Asset, workingDirectory, progress, cancellationToken);
        var scriptPath = WriteApplyScript(workingDirectory);

        progress?.Report("正在准备覆盖旧版本...");
        StartApplyScript(package.Kind, scriptPath, packagePath);
    }

    private static AutoUpdatePackage? ToPackage(ReleaseAsset? asset, AutoUpdatePackageKind kind)
    {
        return asset is null ? null : new AutoUpdatePackage(asset, kind);
    }

    private static ReleaseAsset? FindAsset(UpdateCheckResult result, string extension)
    {
        return result.Assets
            .Where(asset => asset.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(asset => asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
            .ThenBy(asset => asset.Name.Contains("portable", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static bool IsLikelyInstalledWithMsi()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return !string.IsNullOrWhiteSpace(programFiles)
            && IsPathInside(programFiles, Application.ExecutablePath);
    }

    private static bool IsPathInside(string root, string path)
    {
        var rootFullPath = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var pathFullPath = Path.GetFullPath(path);
        return pathFullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateWorkingDirectory(AutoUpdatePackage package)
    {
        var versionPart = string.IsNullOrWhiteSpace(AppInfo.CurrentVersion) ? "update" : AppInfo.CurrentVersion;
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            AppInfo.Name,
            "updates",
            $"{versionPart}-{package.Kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        return workingDirectory;
    }

    private static async Task<string> DownloadPackageAsync(
        ReleaseAsset asset,
        string workingDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(asset.Name);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("更新包文件名无效。");
        }

        var packagePath = Path.Combine(workingDirectory, fileName);
        progress?.Report($"正在下载更新包 {fileName}...");

        using var response = await Client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            packagePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[81920];
        long totalRead = 0;
        while (true)
        {
            var read = await contentStream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            if (totalBytes > 0)
            {
                var percent = Math.Clamp((int)(totalRead * 100 / totalBytes), 0, 100);
                progress?.Report($"正在下载更新包 {fileName}... {percent}%");
            }
        }

        return packagePath;
    }

    private static string WriteApplyScript(string workingDirectory)
    {
        var scriptPath = Path.Combine(workingDirectory, "apply-update.ps1");
        File.WriteAllText(scriptPath, ApplyScript, Encoding.Unicode);
        return scriptPath;
    }

    private static void StartApplyScript(AutoUpdatePackageKind kind, string scriptPath, string packagePath)
    {
        var targetPath = AppInfo.LauncherExecutablePath;
        if (!File.Exists(targetPath))
        {
            throw new InvalidOperationException("无法定位当前程序文件。");
        }

        var requiresElevation = kind == AutoUpdatePackageKind.Msi || !CanWriteTargetDirectory(targetPath);
        var arguments = string.Join(
            " ",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            QuoteArgument(scriptPath),
            "-Mode",
            QuoteArgument(kind.ToString()),
            "-ProcessId",
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            "-SourcePath",
            QuoteArgument(packagePath),
            "-TargetPath",
            QuoteArgument(targetPath));

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            Arguments = arguments,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        var scriptWorkingDirectory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrWhiteSpace(scriptWorkingDirectory))
        {
            // Keep the updater process out of the current app folder so portable updates can replace it.
            startInfo.WorkingDirectory = scriptWorkingDirectory;
        }

        if (requiresElevation)
        {
            startInfo.Verb = "runas";
        }

        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException("无法启动自动更新程序。");
        }
    }

    private static bool CanWriteTargetDirectory(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var testPath = Path.Combine(directory, $".blockscreen-update-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(testPath, string.Empty);
            File.Delete(testPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.CurrentVersion));
        return client;
    }

    private const string ApplyScript = """
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Exe', 'Msi', 'Zip')]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [int]$ProcessId,

    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$TargetPath
)

$ErrorActionPreference = 'Stop'
$backupPath = "$TargetPath.old"
$scriptWorkingDirectory = Split-Path -Parent $PSCommandPath

function Show-Failure {
    param([string]$Message)

    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("Automatic update failed: $Message", "blockScreen", 'OK', 'Warning') | Out-Null
}

try {
    if (-not [string]::IsNullOrWhiteSpace($scriptWorkingDirectory)) {
        Set-Location -LiteralPath $scriptWorkingDirectory
    }

    if ($ProcessId -gt 0) {
        try {
            Wait-Process -Id $ProcessId -Timeout 120 -ErrorAction SilentlyContinue
        }
        catch {
        }
    }

    Start-Sleep -Milliseconds 800

    if ($Mode -eq 'Msi') {
        $arguments = "/i `"$SourcePath`" /passive"
        $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
            throw "Installer exited with code $($process.ExitCode)."
        }

        if (Test-Path -LiteralPath $TargetPath) {
            Start-Process -FilePath $TargetPath
        }

        exit 0
    }

    if ($Mode -eq 'Zip') {
        $targetDirectory = Split-Path -Parent $TargetPath
        $extractPath = Join-Path (Split-Path -Parent $SourcePath) "extracted"
        $newLauncherPath = Join-Path $extractPath (Split-Path -Leaf $TargetPath)
        $newAppPath = Join-Path $extractPath "app"
        $targetAppPath = Join-Path $targetDirectory "app"
        $newPortableReadmePath = Join-Path $extractPath "PORTABLE.txt"
        $targetPortableReadmePath = Join-Path $targetDirectory "PORTABLE.txt"

        if (Test-Path -LiteralPath $extractPath) {
            Remove-Item -LiteralPath $extractPath -Recurse -Force
        }

        Expand-Archive -LiteralPath $SourcePath -DestinationPath $extractPath -Force

        if (-not (Test-Path -LiteralPath $newLauncherPath)) {
            throw "Portable update package does not contain $(Split-Path -Leaf $TargetPath)."
        }

        if (-not (Test-Path -LiteralPath $newAppPath)) {
            throw "Portable update package does not contain app folder."
        }

        if (Test-Path -LiteralPath $backupPath) {
            Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
        }

        Move-Item -LiteralPath $TargetPath -Destination $backupPath -Force

        try {
            Copy-Item -LiteralPath $newLauncherPath -Destination $TargetPath -Force

            if (Test-Path -LiteralPath $targetAppPath) {
                Remove-Item -LiteralPath $targetAppPath -Recurse -Force
            }

            Copy-Item -LiteralPath $newAppPath -Destination $targetAppPath -Recurse -Force

            if (Test-Path -LiteralPath $newPortableReadmePath) {
                Copy-Item -LiteralPath $newPortableReadmePath -Destination $targetPortableReadmePath -Force
            }

            Unblock-File -LiteralPath $TargetPath -ErrorAction SilentlyContinue
            Start-Process -FilePath $TargetPath
            Start-Sleep -Seconds 5
            Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            exit 0
        }
        catch {
            if (Test-Path -LiteralPath $backupPath) {
                Move-Item -LiteralPath $backupPath -Destination $TargetPath -Force -ErrorAction SilentlyContinue
            }

            throw
        }
    }

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Downloaded update package was not found."
    }

    if (Test-Path -LiteralPath $backupPath) {
        Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $TargetPath) {
        Move-Item -LiteralPath $TargetPath -Destination $backupPath -Force
    }

    Copy-Item -LiteralPath $SourcePath -Destination $TargetPath -Force
    Unblock-File -LiteralPath $TargetPath -ErrorAction SilentlyContinue
    Start-Process -FilePath $TargetPath
    Start-Sleep -Seconds 5
    Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
}
catch {
    if ($Mode -eq 'Exe' -and (Test-Path -LiteralPath $backupPath) -and -not (Test-Path -LiteralPath $TargetPath)) {
        Move-Item -LiteralPath $backupPath -Destination $TargetPath -Force -ErrorAction SilentlyContinue
    }

    Show-Failure $_.Exception.Message
    exit 1
}
""";
}

internal enum AutoUpdatePackageKind
{
    Exe,
    Msi,
    Zip
}

internal sealed record AutoUpdatePackage(ReleaseAsset Asset, AutoUpdatePackageKind Kind)
{
    public string Description => Kind switch
    {
        AutoUpdatePackageKind.Msi => "下载 MSI 安装包并覆盖安装",
        AutoUpdatePackageKind.Zip => "下载便携 ZIP 并在退出后覆盖当前文件夹",
        _ => "下载单文件 EXE 并在退出后替换当前程序"
    };
}
