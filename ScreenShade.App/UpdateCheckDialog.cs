namespace ScreenShade.App;

internal static class UpdateCheckDialog
{
    public static async Task CheckAndShowAsync(IWin32Window? owner = null)
    {
        try
        {
            var result = await UpdateChecker.CheckLatestReleaseAsync();
            await ShowResultAsync(owner, result);
        }
        catch (Exception ex)
        {
            ShowMessage(owner, $"检查更新失败：{ex.Message}", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public static string GetStatusText(UpdateCheckResult result)
    {
        if (result.ReleaseNotFound)
        {
            return "GitHub 仓库暂未发布 Release。";
        }

        if (result.IsUpdateAvailable)
        {
            return $"发现新版本 {result.LatestVersion}。";
        }

        return "当前已是最新版本。";
    }

    public static async Task ShowResultAsync(
        IWin32Window? owner,
        UpdateCheckResult result,
        IProgress<string>? progress = null)
    {
        if (result.ReleaseNotFound)
        {
            ShowMessage(
                owner,
                $"当前版本：{result.CurrentVersion}\nGitHub 仓库暂未发布 Release。",
                "检查更新",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (result.IsUpdateAvailable)
        {
            var message = $"发现新版本：{result.LatestVersion}\n当前版本：{result.CurrentVersion}";
            if (result.PublishedAt.HasValue)
            {
                message += $"\n发布时间：{result.PublishedAt.Value.LocalDateTime:yyyy-MM-dd HH:mm}";
            }

            var package = AutoUpdater.SelectPackage(result);
            if (package is null)
            {
                message += "\n\n未找到可自动安装的更新包，是否打开发布页面？";

                if (ShowMessage(owner, message, "检查更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes
                    && !string.IsNullOrWhiteSpace(result.ReleaseUrl))
                {
                    ExternalLink.Open(result.ReleaseUrl, owner);
                }

                return;
            }

            message += $"\n更新包：{package.Asset.Name}";
            message += $"\n更新方式：{package.Description}";
            message += "\n\n选择“是”自动下载并覆盖旧版本；选择“否”打开发布页面。";

            var choice = ShowMessage(owner, message, "检查更新", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
            if (choice == DialogResult.Yes)
            {
                await StartAutoUpdateAsync(owner, package, progress);
            }
            else if (choice == DialogResult.No && !string.IsNullOrWhiteSpace(result.ReleaseUrl))
            {
                ExternalLink.Open(result.ReleaseUrl, owner);
            }

            return;
        }

        ShowMessage(
            owner,
            $"当前已是最新版本。\n当前版本：{result.CurrentVersion}\n最新版本：{result.LatestVersion}",
            "检查更新",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static async Task StartAutoUpdateAsync(
        IWin32Window? owner,
        AutoUpdatePackage package,
        IProgress<string>? progress)
    {
        try
        {
            await AutoUpdater.DownloadAndApplyAsync(package, progress);
            ShowMessage(
                owner,
                "更新包已下载。程序将退出并覆盖旧版本，完成后会自动重新启动。",
                "自动更新",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Application.Exit();
        }
        catch (Exception ex)
        {
            progress?.Report("自动更新失败。");
            ShowMessage(owner, $"自动更新失败：{ex.Message}", "自动更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static DialogResult ShowMessage(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return owner is null
            ? MessageBox.Show(text, caption, buttons, icon)
            : MessageBox.Show(owner, text, caption, buttons, icon);
    }
}
