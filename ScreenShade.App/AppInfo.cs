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
    public const string LicenseName = "未声明许可证";
    public const string LicenseDescription = "当前仓库未包含许可证文件。使用、分发或修改前请先确认作者授权。";

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

            var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
            return string.IsNullOrWhiteSpace(fileVersion) ? "0.0.0" : fileVersion;
        }
    }
}
