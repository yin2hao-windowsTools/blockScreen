namespace ScreenShade.App;

internal sealed class ScreenShadeApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore = new();
    private readonly GlobalHotKeyWindow _hotKeyWindow;
    private readonly Icon _appIcon = AppIcon.Load();
    private readonly NotifyIcon _notifyIcon;
    private readonly OverlayController _overlayController = new();
    private ToolStripMenuItem? _shadeMenuItem;
    private ManagementForm? _managementForm;

    public ScreenShadeApplicationContext()
    {
        _hotKeyWindow = new GlobalHotKeyWindow(() => _overlayController.ToggleShade(_settingsStore.Settings));
        _notifyIcon = CreateNotifyIcon();
        _notifyIcon.Visible = true;
        _overlayController.StateChanged += (_, _) => UpdateTrayMenu();

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
        contextMenu.Items.Add("打开管理页面", null, (_, _) => ShowManagementForm());
        _shadeMenuItem = new ToolStripMenuItem("启动遮罩", null, (_, _) => _overlayController.ToggleShade(_settingsStore.Settings));
        contextMenu.Items.Add(_shadeMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => ExitThread());

        var notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = _appIcon,
            Text = "A1 Screen Shade",
            Visible = false
        };

        notifyIcon.DoubleClick += (_, _) => ShowManagementForm();
        return notifyIcon;
    }

    private void ShowManagementForm()
    {
        if (_managementForm is null || _managementForm.IsDisposed)
        {
            _managementForm = new ManagementForm(_settingsStore, _overlayController, _appIcon);
            _managementForm.FormClosed += (_, _) => _managementForm = null;
        }

        _managementForm.Show();
        _managementForm.Activate();
    }

    private void UpdateTrayMenu()
    {
        if (_shadeMenuItem is null)
        {
            return;
        }

        _shadeMenuItem.Text = _overlayController.IsActive
            ? "关闭遮罩"
            : _overlayController.IsPending
                ? "取消延时"
                : "启动遮罩";
    }

    protected override void ExitThreadCore()
    {
        _managementForm?.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _hotKeyWindow.Dispose();
        _overlayController.Dispose();
        _appIcon.Dispose();
        base.ExitThreadCore();
    }
}
