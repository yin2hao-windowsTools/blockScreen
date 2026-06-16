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
    private QuickDelayForm? _quickDelayForm;
    private AboutForm? _aboutForm;

    public ScreenShadeApplicationContext()
    {
        _hotKeyWindow = new GlobalHotKeyWindow(
            () => _overlayController.ToggleShade(_settingsStore.Settings),
            ShowQuickDelayForm);
        _hotKeyWindow.Apply(_settingsStore.Settings);
        _notifyIcon = CreateNotifyIcon();
        _notifyIcon.Visible = true;
        _settingsStore.SettingsChanged += SettingsStore_SettingsChanged;
        _overlayController.StateChanged += (_, _) => UpdateTrayMenu();

        if (HasConfiguredHotKeyRegistrationFailure())
        {
            ShowHotKeyWarning();
        }
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("打开管理页面", null, (_, _) => PostTrayMenuAction(contextMenu, ShowManagementForm));
        contextMenu.Items.Add("快速定时黑屏", null, (_, _) => PostTrayMenuAction(contextMenu, ShowQuickDelayForm));
        _shadeMenuItem = new ToolStripMenuItem("启动遮罩", null, (_, _) => _overlayController.ToggleShade(_settingsStore.Settings));
        contextMenu.Items.Add(_shadeMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateAboutMenu());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => ExitThread());

        var notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = _appIcon,
            Text = AppInfo.Name,
            Visible = false
        };

        notifyIcon.DoubleClick += (_, _) => ShowManagementForm();
        return notifyIcon;
    }

    private static void PostTrayMenuAction(ContextMenuStrip contextMenu, Action action)
    {
        if (contextMenu.IsDisposed)
        {
            return;
        }

        contextMenu.BeginInvoke((MethodInvoker)(() => action()));
    }

    private ToolStripMenuItem CreateAboutMenu()
    {
        var menu = new ToolStripMenuItem("关于");
        menu.DropDownItems.Add($"关于 {AppInfo.Name}", null, (_, _) => ShowAboutForm());
        menu.DropDownItems.Add("开发者主页", null, (_, _) => ExternalLink.Open(AppInfo.DeveloperHomeUrl));
        menu.DropDownItems.Add("项目主页", null, (_, _) => ExternalLink.Open(AppInfo.RepositoryUrl));
        menu.DropDownItems.Add("检查更新", null, async (_, _) => await UpdateCheckDialog.CheckAndShowAsync());
        menu.DropDownItems.Add("开源许可证", null, (_, _) => ShowLicenseInfo());
        return menu;
    }

    private void ShowManagementForm()
    {
        if (_managementForm is null || _managementForm.IsDisposed)
        {
            _managementForm = new ManagementForm(_settingsStore, _overlayController, _appIcon);
            _managementForm.FormClosed += (_, _) => _managementForm = null;
        }

        ShowAndActivate(_managementForm);
    }

    private void ShowQuickDelayForm()
    {
        if (_quickDelayForm is null || _quickDelayForm.IsDisposed)
        {
            _quickDelayForm = new QuickDelayForm(_settingsStore, _overlayController, _appIcon);
            _quickDelayForm.FormClosed += (_, _) => _quickDelayForm = null;
        }

        ShowAndActivate(_quickDelayForm);
    }

    private void ShowAboutForm()
    {
        if (_aboutForm is null || _aboutForm.IsDisposed)
        {
            _aboutForm = new AboutForm(_appIcon);
            _aboutForm.FormClosed += (_, _) => _aboutForm = null;
        }

        ShowAndActivate(_aboutForm);
    }

    private static void ShowAndActivate(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Show();
        form.BringToFront();
        form.Activate();
    }

    private static void ShowLicenseInfo()
    {
        MessageBox.Show(
            $"{AppInfo.LicenseName}\n\n{AppInfo.LicenseDescription}",
            "开源许可证",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void SettingsStore_SettingsChanged(object? sender, EventArgs e)
    {
        _hotKeyWindow.Apply(_settingsStore.Settings);

        if (HasConfiguredHotKeyRegistrationFailure())
        {
            ShowHotKeyWarning();
        }
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
        _quickDelayForm?.Close();
        _aboutForm?.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _settingsStore.SettingsChanged -= SettingsStore_SettingsChanged;
        _hotKeyWindow.Dispose();
        _overlayController.Dispose();
        _appIcon.Dispose();
        base.ExitThreadCore();
    }

    private void ShowHotKeyWarning()
    {
        _notifyIcon.ShowBalloonTip(
            3000,
            AppInfo.Name,
            "部分快捷键注册失败，请在管理页面更换未被占用的组合键。",
            ToolTipIcon.Warning);
    }

    private bool HasConfiguredHotKeyRegistrationFailure()
    {
        return (_hotKeyWindow.IsToggleShadeConfigured && !_hotKeyWindow.IsToggleShadeRegistered)
            || (_hotKeyWindow.IsQuickDelayConfigured && !_hotKeyWindow.IsQuickDelayRegistered);
    }
}
