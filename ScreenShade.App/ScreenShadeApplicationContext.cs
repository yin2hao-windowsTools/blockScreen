namespace ScreenShade.App;

internal sealed class ScreenShadeApplicationContext : ApplicationContext
{
    private readonly GlobalHotKeyWindow _hotKeyWindow;
    private readonly NotifyIcon _notifyIcon;
    private readonly OverlayController _overlayController = new();

    public ScreenShadeApplicationContext()
    {
        _hotKeyWindow = new GlobalHotKeyWindow(_overlayController.ToggleShade);
        _notifyIcon = CreateNotifyIcon();
        _notifyIcon.Visible = true;

        if (!_hotKeyWindow.IsRegistered)
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                "A1 Screen Shade",
                "Ctrl+Alt+B 热键注册失败，可通过托盘菜单启用遮罩。",
                ToolTipIcon.Warning);
        }
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("启用遮罩", null, (_, _) => _overlayController.ShowShade());
        contextMenu.Items.Add("退出", null, (_, _) => ExitThread());

        var notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = SystemIcons.Application,
            Text = "A1 Screen Shade",
            Visible = false
        };

        notifyIcon.DoubleClick += (_, _) => _overlayController.ToggleShade();
        return notifyIcon;
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _hotKeyWindow.Dispose();
        _overlayController.Dispose();
        base.ExitThreadCore();
    }
}
