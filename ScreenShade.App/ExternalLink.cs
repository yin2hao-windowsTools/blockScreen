using System.Diagnostics;

namespace ScreenShade.App;

internal static class ExternalLink
{
    public static void Open(string url, IWin32Window? owner = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowMessage(owner, $"无法打开链接：{ex.Message}", "打开链接失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
