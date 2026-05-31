using System.Drawing.Drawing2D;

namespace ScreenShade.App;

internal sealed class ManagementForm : Form
{
    private static readonly Color PageBackColor = Color.FromArgb(246, 248, 252);
    private static readonly Color CardBorderColor = Color.FromArgb(220, 226, 235);
    private static readonly Color AccentColor = Color.FromArgb(24, 119, 242);
    private static readonly Color PrimaryTextColor = Color.FromArgb(17, 24, 39);
    private static readonly Color SecondaryTextColor = Color.FromArgb(107, 114, 128);

    private readonly SettingsStore _settingsStore;
    private readonly OverlayController _overlayController;
    private readonly DataGridView _displayGrid = new();
    private readonly NumericUpDown _delayInput = new();
    private readonly CheckBox _brightnessCheckBox = new();
    private readonly CheckBox _startupCheckBox = new();
    private readonly CheckBox _exitOnMouseMoveCheckBox = new();
    private readonly HotKeyInputBox _toggleHotKeyInput = new();
    private readonly HotKeyInputBox _quickDelayHotKeyInput = new();
    private readonly Button _startShadeButton = new();
    private readonly Button _delayShadeButton = new();
    private readonly Icon _icon;

    public ManagementForm(SettingsStore settingsStore, OverlayController overlayController, Icon icon)
    {
        _settingsStore = settingsStore;
        _overlayController = overlayController;
        _icon = icon;

        AutoScaleDimensions = new SizeF(9F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = PageBackColor;
        ClientSize = new Size(1260, 860);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Icon = icon;
        MinimumSize = new Size(1120, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Text = $"{AppInfo.Name} 管理";

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
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(118, 54),
            Padding = new Point(18, 10),
            SizeMode = TabSizeMode.Fixed
        };
        tabs.DrawItem += Tabs_DrawItem;

        var managementPage = new TabPage("管理")
        {
            BackColor = PageBackColor,
            Padding = new Padding(0)
        };
        managementPage.Controls.Add(BuildManagementPage());

        var aboutPage = new TabPage("关于")
        {
            BackColor = PageBackColor,
            Padding = new Padding(0)
        };
        aboutPage.Controls.Add(new AboutPanel(_icon));

        tabs.TabPages.Add(managementPage);
        tabs.TabPages.Add(aboutPage);
        Controls.Add(tabs);
    }

    private Control BuildManagementPage()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(26),
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 206));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        var displayCard = CreateCard("显示器", BuildDisplayGrid(), CreateRefreshButton());
        displayCard.Margin = new Padding(0, 0, 0, 18);
        root.Controls.Add(displayCard, 0, 0);

        var settingsCard = CreateCard("设置", BuildSettingsPanel());
        settingsCard.Margin = new Padding(0, 0, 0, 18);
        root.Controls.Add(settingsCard, 0, 1);

        var hotKeyCard = CreateCard("快捷键", BuildHotKeyPanel());
        hotKeyCard.Margin = new Padding(0, 0, 0, 18);
        root.Controls.Add(hotKeyCard, 0, 2);

        root.Controls.Add(BuildBottomButtons(), 0, 3);
        return root;
    }

    private Control BuildDisplayGrid()
    {
        _displayGrid.AllowUserToAddRows = false;
        _displayGrid.AllowUserToDeleteRows = false;
        _displayGrid.AllowUserToResizeColumns = false;
        _displayGrid.AllowUserToResizeRows = false;
        _displayGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _displayGrid.BackgroundColor = Color.White;
        _displayGrid.BorderStyle = BorderStyle.FixedSingle;
        _displayGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _displayGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _displayGrid.ColumnHeadersHeight = 54;
        _displayGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _displayGrid.Dock = DockStyle.Fill;
        _displayGrid.EditMode = DataGridViewEditMode.EditOnEnter;
        _displayGrid.EnableHeadersVisualStyles = false;
        _displayGrid.GridColor = CardBorderColor;
        _displayGrid.MultiSelect = false;
        _displayGrid.RowHeadersVisible = false;
        _displayGrid.RowTemplate.Height = 56;
        _displayGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _displayGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            BackColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            SelectionBackColor = Color.White,
            SelectionForeColor = Color.FromArgb(30, 41, 59)
        };

        _displayGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(31, 41, 55),
            Padding = new Padding(8, 0, 8, 0),
            SelectionBackColor = Color.FromArgb(240, 247, 255),
            SelectionForeColor = Color.FromArgb(31, 41, 55)
        };

        var displayColumn = new DataGridViewColumn(new DisplayCheckBoxCell())
        {
            FillWeight = 28,
            HeaderText = "显示器",
            MinimumWidth = 180,
            Name = "Display",
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var typeColumn = CreateTextColumn("Type", "类型", 24);
        var resolutionColumn = CreateTextColumn("Resolution", "分辨率", 30);
        var locationColumn = CreateTextColumn("Location", "位置", 38);

        typeColumn.DefaultCellStyle = new DataGridViewCellStyle(_displayGrid.DefaultCellStyle)
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter
        };
        resolutionColumn.DefaultCellStyle = new DataGridViewCellStyle(_displayGrid.DefaultCellStyle)
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter
        };

        _displayGrid.Columns.AddRange(displayColumn, typeColumn, resolutionColumn, locationColumn);
        _displayGrid.CurrentCellDirtyStateChanged += DisplayGrid_CurrentCellDirtyStateChanged;
        _displayGrid.CellMouseUp += DisplayGrid_CellMouseUp;
        return _displayGrid;
    }

    private Control BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var delayLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0, 10, 14, 0),
            Text = "延时启动(秒)",
            TextAlign = ContentAlignment.TopLeft
        };
        panel.Controls.Add(delayLabel, 0, 0);

        _delayInput.Anchor = AnchorStyles.Left;
        _delayInput.Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point);
        _delayInput.Maximum = 3600;
        _delayInput.Minimum = 0;
        _delayInput.Size = new Size(160, 34);
        _delayInput.TextAlign = HorizontalAlignment.Right;
        panel.Controls.Add(_delayInput, 1, 0);

        _brightnessCheckBox.Anchor = AnchorStyles.Left;
        _brightnessCheckBox.AutoSize = true;
        _brightnessCheckBox.Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point);
        _brightnessCheckBox.Margin = new Padding(18, 8, 0, 0);
        _brightnessCheckBox.Text = "同时降低硬件亮度";
        panel.Controls.Add(_brightnessCheckBox, 2, 0);
        panel.SetColumnSpan(_brightnessCheckBox, 2);

        var optionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 10, 0, 0),
            WrapContents = true
        };

        _startupCheckBox.AutoSize = true;
        _startupCheckBox.Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point);
        _startupCheckBox.Margin = new Padding(0, 0, 28, 0);
        _startupCheckBox.Text = "开机自启动";
        optionPanel.Controls.Add(_startupCheckBox);

        _exitOnMouseMoveCheckBox.AutoSize = true;
        _exitOnMouseMoveCheckBox.Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point);
        _exitOnMouseMoveCheckBox.Margin = new Padding(0);
        _exitOnMouseMoveCheckBox.Text = "鼠标移动时退出遮罩";
        optionPanel.Controls.Add(_exitOnMouseMoveCheckBox);

        panel.Controls.Add(optionPanel, 0, 1);
        panel.SetColumnSpan(optionPanel, 4);
        return panel;
    }

    private Control BuildHotKeyPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        AddHotKeyRow(panel, 0, "切换黑屏", _toggleHotKeyInput, "默认 Ctrl+Alt...");
        AddHotKeyRow(panel, 1, "快速定时菜单", _quickDelayHotKeyInput, "默认 Ctrl+Alt...");
        return panel;
    }

    private Control BuildBottomButtons()
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 6,
            Dock = DockStyle.Fill,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigurePrimaryButton(_startShadeButton, "启动遮罩", 186);
        _startShadeButton.Click += (_, _) => StartShade();
        layout.Controls.Add(_startShadeButton, 1, 0);

        ConfigureSecondaryButton(_delayShadeButton, "延时启动遮罩", 186);
        _delayShadeButton.Click += (_, _) => StartDelayedShade();
        layout.Controls.Add(_delayShadeButton, 2, 0);

        var saveButton = CreateSecondaryButton("保存", (_, _) => SaveSettings(), 134);
        layout.Controls.Add(saveButton, 3, 0);

        var closeButton = CreateSecondaryButton("关闭", (_, _) => Close(), 134);
        layout.Controls.Add(closeButton, 4, 0);

        foreach (Control control in layout.Controls)
        {
            control.Margin = new Padding(0, 10, 18, 0);
        }

        closeButton.Margin = new Padding(0, 10, 0, 0);
        return layout;
    }

    private Control CreateCard(string title, Control body, Control? headerAction = null)
    {
        var card = new RoundedPanel
        {
            BorderColor = CardBorderColor,
            CornerRadius = 12,
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            Padding = new Padding(24, 20, 24, 24)
        };

        var content = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 18F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        };
        content.Controls.Add(titleLabel, 0, 0);

        if (headerAction is not null)
        {
            headerAction.Anchor = AnchorStyles.Right;
            headerAction.Margin = new Padding(16, 0, 0, 0);
            content.Controls.Add(headerAction, 1, 0);
        }

        body.Margin = new Padding(0, 6, 0, 0);
        content.Controls.Add(body, 0, 1);
        content.SetColumnSpan(body, 2);

        card.Controls.Add(content);
        return card;
    }

    private Button CreateSecondaryButton(string text, EventHandler onClick, int width)
    {
        var button = new Button();
        ConfigureSecondaryButton(button, text, width);
        button.Click += onClick;
        return button;
    }

    private Button CreateRefreshButton()
    {
        var button = CreateSecondaryButton("刷新显示器", (_, _) => RefreshDisplayList(), 172);
        button.Image = CreateRefreshIcon(PrimaryTextColor);
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(14, 0, 14, 0);
        button.TextAlign = ContentAlignment.MiddleRight;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        return button;
    }

    private static void ConfigurePrimaryButton(Button button, string text, int width)
    {
        button.AutoSize = false;
        button.BackColor = AccentColor;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderColor = AccentColor;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(18, 102, 223);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 131, 255);
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
        button.ForeColor = Color.White;
        button.MinimumSize = new Size(width, 46);
        button.Size = new Size(width, 46);
        button.Text = text;
        button.UseVisualStyleBackColor = false;
        ApplyRoundedRegion(button, 7);
    }

    private static void ConfigureSecondaryButton(Button button, string text, int width)
    {
        button.AutoSize = false;
        button.BackColor = Color.White;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderColor = CardBorderColor;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(244, 247, 252);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(249, 250, 252);
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
        button.ForeColor = Color.FromArgb(31, 41, 55);
        button.MinimumSize = new Size(width, 46);
        button.Size = new Size(width, 46);
        button.Text = text;
        button.UseVisualStyleBackColor = false;
        ApplyRoundedRegion(button, 7);
    }

    private static Bitmap CreateRefreshIcon(Color color)
    {
        var bitmap = new Bitmap(18, 18);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var pen = new Pen(color, 2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graphics.DrawArc(pen, 3, 3, 12, 12, 35, 290);
        using var brush = new SolidBrush(color);
        var arrow = new[]
        {
            new PointF(13.5F, 2.8F),
            new PointF(16F, 2.2F),
            new PointF(15.3F, 4.8F)
        };
        graphics.FillPolygon(brush, arrow);
        return bitmap;
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        control.Resize += (_, _) => UpdateRoundedRegion(control, radius);
        UpdateRoundedRegion(control, radius);
    }

    private static void UpdateRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region?.Dispose();
        control.Region = new Region(path);
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string name, string headerText, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            FillWeight = fillWeight,
            HeaderText = headerText,
            MinimumWidth = 120,
            Name = name,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            path.CloseFigure();
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter - 1;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter - 1;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Settings;
        _delayInput.Value = Math.Clamp(settings.DelaySeconds, (int)_delayInput.Minimum, (int)_delayInput.Maximum);
        _brightnessCheckBox.Checked = settings.DimHardwareBrightness;
        _exitOnMouseMoveCheckBox.Checked = settings.ExitOnMouseMove;
        _startupCheckBox.Checked = StartupRegistration.IsEnabled();
        _toggleHotKeyInput.HotKey = settings.ToggleShadeHotKey;
        _quickDelayHotKeyInput.HotKey = settings.QuickDelayHotKey;
        RefreshDisplayList();
    }

    private void RefreshDisplayList()
    {
        var checkedDisplayDeviceNames = GetSelectedDisplayDeviceNames();
        if (checkedDisplayDeviceNames.Count == 0)
        {
            checkedDisplayDeviceNames = ScreenShadeSettings.NormalizeDisplayDeviceNames(_settingsStore.Settings.DisplayDeviceNames);
        }

        _displayGrid.Rows.Clear();

        var screens = Screen.AllScreens;
        for (var index = 0; index < screens.Length; index++)
        {
            var item = new DisplayItem(index + 1, screens[index]);
            item.IsSelected = checkedDisplayDeviceNames.Count == 0 || checkedDisplayDeviceNames.Contains(item.DeviceName);
            var rowIndex = _displayGrid.Rows.Add(item.DisplayName, item.DisplayType, item.Resolution, item.DeviceName);
            _displayGrid.Rows[rowIndex].Tag = item;
        }

        _displayGrid.ClearSelection();
    }

    private HashSet<string> GetSelectedDisplayDeviceNames()
    {
        return GetDisplayRows()
            .Where(IsDisplayRowChecked)
            .Select(row => row.Tag)
            .OfType<DisplayItem>()
            .Select(item => item.DeviceName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<DataGridViewRow> GetDisplayRows()
    {
        return _displayGrid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow);
    }

    private static bool IsDisplayRowChecked(DataGridViewRow row)
    {
        return row.Tag is DisplayItem { IsSelected: true };
    }

    private void SaveSettings()
    {
        if (TryBuildSettings(out var settings))
        {
            PersistSettings(settings);
            UpdateState();
        }
    }

    private void StartShade()
    {
        if (_overlayController.IsActive)
        {
            _overlayController.HideShade();
            return;
        }

        if (!TryBuildSettings(out var settings))
        {
            return;
        }

        PersistSettings(settings);
        _overlayController.ShowShade(settings);
    }

    private void StartDelayedShade()
    {
        if (_overlayController.IsPending)
        {
            _overlayController.HideShade();
            return;
        }

        if (!TryBuildSettings(out var settings))
        {
            return;
        }

        if (settings.DelaySeconds <= 0)
        {
            MessageBox.Show(this, "请先将延时启动设置为大于 0 的秒数。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        PersistSettings(settings);
        if (_overlayController.IsActive)
        {
            _overlayController.HideShade();
        }

        _overlayController.ShowShadeWithDelay(settings);
    }

    private void PersistSettings(ScreenShadeSettings settings)
    {
        _settingsStore.Save(settings);
        SaveStartupRegistration();
    }

    private void SaveStartupRegistration()
    {
        try
        {
            StartupRegistration.SetEnabled(_startupCheckBox.Checked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"开机自启动设置保存失败：{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _startupCheckBox.Checked = StartupRegistration.IsEnabled();
        }
    }

    private bool TryBuildSettings(out ScreenShadeSettings settings)
    {
        var selectedDisplays = GetDisplayRows()
            .Where(IsDisplayRowChecked)
            .Select(row => row.Tag)
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
            ExitOnMouseMove = _exitOnMouseMoveCheckBox.Checked,
            ToggleShadeHotKey = _toggleHotKeyInput.HotKey,
            QuickDelayHotKey = _quickDelayHotKeyInput.HotKey,
            DisplayDeviceNames = selectedDisplays.Length == GetDisplayRows().Count()
                ? []
                : [.. selectedDisplays.Select(display => display.DeviceName)]
        };

        if (!settings.ToggleShadeHotKey.IsValid)
        {
            MessageBox.Show(this, "请为切换黑屏设置包含 Ctrl、Alt 或 Shift 的快捷键。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (!settings.QuickDelayHotKey.IsValid)
        {
            MessageBox.Show(this, "请为快速定时菜单设置包含 Ctrl、Alt 或 Shift 的快捷键。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (settings.ToggleShadeHotKey.HasSameGesture(settings.QuickDelayHotKey))
        {
            MessageBox.Show(this, "两个快捷键不能使用同一个组合。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        return true;
    }

    private void AddHotKeyRow(TableLayoutPanel panel, int row, string labelText, HotKeyInputBox inputBox, string hintText)
    {
        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0, 10, 18, 0),
            Text = labelText,
            TextAlign = ContentAlignment.TopLeft
        };
        panel.Controls.Add(label, 0, row);

        inputBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        inputBox.Font = new Font(Font.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point);
        inputBox.Margin = new Padding(0, 4, 24, 0);
        inputBox.MinimumSize = new Size(360, 40);
        inputBox.Size = new Size(360, 40);
        panel.Controls.Add(inputBox, 1, row);

        var hint = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = SecondaryTextColor,
            Margin = new Padding(0, 10, 0, 0),
            Text = hintText,
            TextAlign = ContentAlignment.TopLeft
        };
        panel.Controls.Add(hint, 2, row);
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
            _startShadeButton.Text = "关闭遮罩";
            _delayShadeButton.Text = "延时启动遮罩";
        }
        else if (_overlayController.IsPending)
        {
            _startShadeButton.Text = "立即启动遮罩";
            _delayShadeButton.Text = "取消延时";
        }
        else
        {
            _startShadeButton.Text = "启动遮罩";
            _delayShadeButton.Text = "延时启动遮罩";
        }
    }

    private void DisplayGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_displayGrid.IsCurrentCellDirty)
        {
            _displayGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void DisplayGrid_CellMouseUp(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0 || _displayGrid.Rows[e.RowIndex].Tag is not DisplayItem item)
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        _displayGrid.InvalidateCell(e.ColumnIndex, e.RowIndex);
    }

    private void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabControl || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
        {
            return;
        }

        var bounds = e.Bounds;
        var isSelected = e.Index == tabControl.SelectedIndex;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pageBrush = new SolidBrush(PageBackColor);
        e.Graphics.FillRectangle(pageBrush, bounds);

        var tabBounds = Rectangle.Inflate(bounds, -2, -4);
        using var backgroundBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(250, 251, 253));
        using var borderPen = new Pen(CardBorderColor);
        using var tabPath = CreateRoundedRectanglePath(tabBounds, 8);
        e.Graphics.FillPath(backgroundBrush, tabPath);
        e.Graphics.DrawPath(borderPen, tabPath);

        if (isSelected)
        {
            using var accentPen = new Pen(AccentColor, 3);
            e.Graphics.DrawLine(accentPen, tabBounds.Left + 12, tabBounds.Bottom - 2, tabBounds.Right - 12, tabBounds.Bottom - 2);
        }

        TextRenderer.DrawText(
            e.Graphics,
            tabControl.TabPages[e.Index].Text,
            new Font(Font.FontFamily, 11F, isSelected ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
            bounds,
            Color.FromArgb(15, 23, 42),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private sealed class DisplayCheckBoxCell : DataGridViewTextBoxCell
    {
        protected override void Paint(
            Graphics graphics,
            Rectangle clipBounds,
            Rectangle cellBounds,
            int rowIndex,
            DataGridViewElementStates cellState,
            object? value,
            object? formattedValue,
            string? errorText,
            DataGridViewCellStyle cellStyle,
            DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            base.Paint(
                graphics,
                clipBounds,
                cellBounds,
                rowIndex,
                cellState,
                value,
                formattedValue,
                errorText,
                cellStyle,
                advancedBorderStyle,
                paintParts & ~DataGridViewPaintParts.ContentForeground);

            var checkedState = OwningRow?.Tag is DisplayItem { IsSelected: true };
            var glyphSize = Application.RenderWithVisualStyles
                ? CheckBoxRenderer.GetGlyphSize(graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal)
                : new Size(14, 14);

            var checkboxBounds = new Rectangle(
                cellBounds.Left + 18,
                cellBounds.Top + (cellBounds.Height - glyphSize.Height) / 2,
                glyphSize.Width,
                glyphSize.Height);

            if (Application.RenderWithVisualStyles)
            {
                CheckBoxRenderer.DrawCheckBox(
                    graphics,
                    checkboxBounds.Location,
                    checkedState
                        ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                        : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal);
            }
            else
            {
                ControlPaint.DrawCheckBox(
                    graphics,
                    checkboxBounds,
                    checkedState ? ButtonState.Checked : ButtonState.Normal);
            }

            var textBounds = new Rectangle(
                checkboxBounds.Right + 16,
                cellBounds.Top,
                Math.Max(0, cellBounds.Right - checkboxBounds.Right - 24),
                cellBounds.Height);
            var textColor = cellState.HasFlag(DataGridViewElementStates.Selected)
                ? cellStyle.SelectionForeColor
                : cellStyle.ForeColor;

            TextRenderer.DrawText(
                graphics,
                Convert.ToString(formattedValue),
                cellStyle.Font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class RoundedPanel : Panel
    {
        public RoundedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);
        }

        public Color BorderColor { get; set; } = CardBorderColor;

        public int CornerRadius { get; set; } = 12;

        public Color FillColor { get; set; } = Color.White;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? PageBackColor);

            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            using var brush = new SolidBrush(FillColor);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class DisplayItem(int index, Screen screen)
    {
        public bool IsSelected { get; set; }

        public string DeviceName { get; } = screen.DeviceName;

        public string DisplayName { get; } = $"显示器 {index}";

        public string DisplayType { get; } = screen.Primary ? "主显示器" : "扩展显示器";

        public string Resolution { get; } = $"{screen.Bounds.Width}x{screen.Bounds.Height}";
    }
}
