using System.Diagnostics;
using System.Reflection;

namespace ScreenShade.App;

internal static class AppInfo
{
    public const string Name = "blockScreen";
    public const string DeveloperName = "yin2hao-windowsTools";
    public const string DeveloperHomeUrl = "https://github.com/yin2hao-windowsTools";
    public const string RepositoryUrl = "https://github.com/yin2hao-windowsTools/blockScreen";
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/yin2hao-windowsTools/blockScreen/releases/latest";
    public const string ReleasesUrl = "https://github.com/yin2hao-windowsTools/blockScreen/releases";
    public const string LicenseName = "MIT License";

    public static string LicenseDescription => ReadEmbeddedText("LICENSE")
        ?? "MIT License\n\nCopyright (c) 2026 yin2hao-windowsTools";

    public static string LauncherExecutablePath
    {
        get
        {
            var currentExecutablePath = Application.ExecutablePath;
            var currentDirectory = Path.GetDirectoryName(currentExecutablePath);
            var parentDirectory = string.IsNullOrWhiteSpace(currentDirectory)
                ? null
                : Directory.GetParent(currentDirectory)?.FullName;

            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                var launcherPath = Path.Combine(parentDirectory, $"{Name}.exe");
                if (File.Exists(launcherPath))
                {
                    return launcherPath;
                }
            }

            return currentExecutablePath;
        }
    }

    public static string CurrentVersion
    {
        get
        {
            var assembly = typeof(AppInfo).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }

            var fileVersion = FileVersionInfo.GetVersionInfo(Application.ExecutablePath).ProductVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion))
            {
                return fileVersion;
            }

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    private static string? ReadEmbeddedText(string resourceName)
    {
        var assembly = typeof(AppInfo).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
