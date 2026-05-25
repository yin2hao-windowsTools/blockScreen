namespace ScreenShade.App;

internal static class UpdateCheckDialog
{
    public static async Task CheckAndShowAsync(IWin32Window? owner = null)
    {
        try
        {
            var result = await UpdateChecker.CheckLatestReleaseAsync();
            ShowResult(owner, result);
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

    public static void ShowResult(IWin32Window? owner, UpdateCheckResult result)
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

            message += "\n\n是否打开发布页面？";

            if (ShowMessage(owner, message, "检查更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes
                && !string.IsNullOrWhiteSpace(result.ReleaseUrl))
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
