namespace ScreenShade.App;

internal sealed class ManagementForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly OverlayController _overlayController;
    private readonly CheckedListBox _displayList = new();
    private readonly NumericUpDown _delayInput = new();
    private readonly CheckBox _brightnessCheckBox = new();
    private readonly Label _stateLabel = new();
    private readonly Button _shadeButton = new();

    public ManagementForm(SettingsStore settingsStore, OverlayController overlayController, Icon icon)
    {
        _settingsStore = settingsStore;
        _overlayController = overlayController;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(620, 430);
        Font = SystemFonts.MessageBoxFont;
        Icon = icon;
        MaximizeBox = false;
        MinimumSize = new Size(560, 380);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "A1 Screen Shade 管理";

        BuildLayout();
        LoadSettings();
        UpdateState();

        _overlayController.StateChanged += OverlayController_StateChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _overlayController.StateChanged -= OverlayController_StateChanged;
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _stateLabel.AutoEllipsis = true;
        _stateLabel.Dock = DockStyle.Fill;
        _stateLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_stateLabel, 0, 0);

        var displayGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "显示器"
        };
        displayGroup.Controls.Add(_displayList);

        _displayList.BorderStyle = BorderStyle.None;
        _displayList.CheckOnClick = true;
        _displayList.Dock = DockStyle.Fill;
        _displayList.FormattingEnabled = true;
        _displayList.IntegralHeight = false;

        root.Controls.Add(displayGroup, 0, 1);

        var settingsPanel = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0)
        };
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var delayLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 8, 0),
            Text = "延时启动(秒)"
        };
        settingsPanel.Controls.Add(delayLabel, 0, 0);

        _delayInput.Dock = DockStyle.Top;
        _delayInput.Maximum = 3600;
        _delayInput.Minimum = 0;
        _delayInput.Width = 92;
        settingsPanel.Controls.Add(_delayInput, 1, 0);

        _brightnessCheckBox.AutoSize = true;
        _brightnessCheckBox.Dock = DockStyle.Top;
        _brightnessCheckBox.Margin = new Padding(12, 4, 0, 0);
        _brightnessCheckBox.Text = "同时降低硬件亮度";
        settingsPanel.Controls.Add(_brightnessCheckBox, 2, 0);

        var refreshButton = new Button
        {
            AutoSize = true,
            Text = "刷新显示器",
            UseVisualStyleBackColor = true
        };
        refreshButton.Click += (_, _) => RefreshDisplayList();
        settingsPanel.Controls.Add(refreshButton, 3, 0);

        root.Controls.Add(settingsPanel, 0, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var closeButton = new Button
        {
            Text = "关闭",
            UseVisualStyleBackColor = true
        };
        closeButton.Click += (_, _) => Close();
        buttonPanel.Controls.Add(closeButton);

        var saveButton = new Button
        {
            Text = "保存",
            UseVisualStyleBackColor = true
        };
        saveButton.Click += (_, _) => SaveSettings();
        buttonPanel.Controls.Add(saveButton);

        _shadeButton.Text = "启动遮罩";
        _shadeButton.UseVisualStyleBackColor = true;
        _shadeButton.Click += (_, _) => ToggleShade();
        buttonPanel.Controls.Add(_shadeButton);

        root.Controls.Add(buttonPanel, 0, 3);
        Controls.Add(root);
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Settings;
        _delayInput.Value = Math.Clamp(settings.DelaySeconds, (int)_delayInput.Minimum, (int)_delayInput.Maximum);
        _brightnessCheckBox.Checked = settings.DimHardwareBrightness;
        RefreshDisplayList();
    }

    private void RefreshDisplayList()
    {
        var checkedDisplayDeviceNames = _displayList.CheckedItems
            .OfType<DisplayItem>()
            .Select(item => item.DeviceName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (checkedDisplayDeviceNames.Count == 0)
        {
            checkedDisplayDeviceNames = ScreenShadeSettings.NormalizeDisplayDeviceNames(_settingsStore.Settings.DisplayDeviceNames);
        }

        _displayList.BeginUpdate();
        _displayList.Items.Clear();

        var screens = Screen.AllScreens;
        for (var index = 0; index < screens.Length; index++)
        {
            var item = new DisplayItem(index + 1, screens[index]);
            var isChecked = checkedDisplayDeviceNames.Count == 0 || checkedDisplayDeviceNames.Contains(item.DeviceName);
            _displayList.Items.Add(item, isChecked);
        }

        _displayList.EndUpdate();
    }

    private void SaveSettings()
    {
        if (TryBuildSettings(out var settings))
        {
            _settingsStore.Save(settings);
            UpdateState();
        }
    }

    private void ToggleShade()
    {
        if (_overlayController.IsActive || _overlayController.IsPending)
        {
            _overlayController.HideShade();
            return;
        }

        if (!TryBuildSettings(out var settings))
        {
            return;
        }

        _settingsStore.Save(settings);
        _overlayController.ShowShade(settings);
    }

    private bool TryBuildSettings(out ScreenShadeSettings settings)
    {
        var selectedDisplays = _displayList.CheckedItems
            .OfType<DisplayItem>()
            .ToArray();

        if (selectedDisplays.Length == 0)
        {
            MessageBox.Show(this, "请至少选择一个显示器。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            settings = new ScreenShadeSettings();
            return false;
        }

        settings = new ScreenShadeSettings
        {
            DelaySeconds = (int)_delayInput.Value,
            DimHardwareBrightness = _brightnessCheckBox.Checked,
            DisplayDeviceNames = selectedDisplays.Length == _displayList.Items.Count
                ? []
                : [.. selectedDisplays.Select(display => display.DeviceName)]
        };

        return true;
    }

    private void OverlayController_StateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)UpdateState);
            return;
        }

        UpdateState();
    }

    private void UpdateState()
    {
        if (_overlayController.IsActive)
        {
            _stateLabel.Text = "状态: 遮罩已启动";
            _shadeButton.Text = "关闭遮罩";
        }
        else if (_overlayController.IsPending)
        {
            _stateLabel.Text = "状态: 等待延时结束后启动遮罩";
            _shadeButton.Text = "取消延时";
        }
        else
        {
            _stateLabel.Text = "状态: 未启动";
            _shadeButton.Text = "启动遮罩";
        }
    }

    private sealed class DisplayItem(int index, Screen screen)
    {
        public string DeviceName { get; } = screen.DeviceName;

        public override string ToString()
        {
            var primaryText = screen.Primary ? "主显示器" : "扩展显示器";
            return $"显示器 {index}  {primaryText}  {screen.Bounds.Width}x{screen.Bounds.Height}  {screen.DeviceName}";
        }
    }
}
