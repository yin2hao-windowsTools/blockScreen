namespace ScreenShade.App;

internal sealed class QuickDelayForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly OverlayController _overlayController;
    private readonly NumericUpDown _delayInput = new();

    public QuickDelayForm(SettingsStore settingsStore, OverlayController overlayController, Icon icon)
    {
        _settingsStore = settingsStore;
        _overlayController = overlayController;

        AutoScaleDimensions = new SizeF(9F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(420, 230);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Icon = icon;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "快速定时黑屏";
        TopMost = true;

        BuildLayout();
        LoadSettings();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _delayInput.Focus();
        _delayInput.Select(0, _delayInput.Text.Length);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var title = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            Text = "设置延时后启动黑屏",
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);

        var delayPanel = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill
        };
        delayPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        delayPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        delayPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var delayLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 10, 0),
            Text = "延时"
        };
        delayPanel.Controls.Add(delayLabel, 0, 0);

        _delayInput.Dock = DockStyle.Top;
        _delayInput.Maximum = 3600;
        _delayInput.Minimum = 1;
        _delayInput.TextAlign = HorizontalAlignment.Right;
        delayPanel.Controls.Add(_delayInput, 1, 0);

        var unitLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 8, 0, 0),
            Text = "秒"
        };
        delayPanel.Controls.Add(unitLabel, 2, 0);

        root.Controls.Add(delayPanel, 0, 1);

        var presetPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        AddPresetButton(presetPanel, "30 秒", 30);
        AddPresetButton(presetPanel, "1 分钟", 60);
        AddPresetButton(presetPanel, "5 分钟", 300);
        AddPresetButton(presetPanel, "10 分钟", 600);
        root.Controls.Add(presetPanel, 0, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            MinimumSize = new Size(88, 34),
            Text = "取消",
            UseVisualStyleBackColor = true
        };
        buttonPanel.Controls.Add(cancelButton);

        var startButton = new Button
        {
            DialogResult = DialogResult.OK,
            MinimumSize = new Size(108, 34),
            Text = "开始定时",
            UseVisualStyleBackColor = true
        };
        startButton.Click += (_, _) => StartTimedShade();
        buttonPanel.Controls.Add(startButton);

        AcceptButton = startButton;
        CancelButton = cancelButton;
        root.Controls.Add(buttonPanel, 0, 3);
        Controls.Add(root);
    }

    private void AddPresetButton(FlowLayoutPanel panel, string text, int seconds)
    {
        var button = new Button
        {
            Margin = new Padding(0, 8, 10, 0),
            MinimumSize = new Size(82, 34),
            Text = text,
            UseVisualStyleBackColor = true
        };
        button.Click += (_, _) => _delayInput.Value = seconds;
        panel.Controls.Add(button);
    }

    private void LoadSettings()
    {
        var delaySeconds = Math.Clamp(_settingsStore.Settings.DelaySeconds, (int)_delayInput.Minimum, (int)_delayInput.Maximum);
        _delayInput.Value = delaySeconds;
    }

    private void StartTimedShade()
    {
        var settings = _settingsStore.Settings.Clone();
        settings.DelaySeconds = (int)_delayInput.Value;
        _settingsStore.Save(settings);

        if (_overlayController.IsActive)
        {
            _overlayController.HideShade();
        }

        _overlayController.ShowShade(settings);
        Close();
    }
}
