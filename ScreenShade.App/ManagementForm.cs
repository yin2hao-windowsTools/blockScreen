using System.Drawing.Drawing2D;
using System.Management;
using System.Runtime.InteropServices;

namespace ScreenShade.App;

internal sealed class ManagementForm : Form
{
    private static readonly Color PageBackColor = Color.FromArgb(243, 246, 251);
    private static readonly Color CardBackColor = Color.White;
    private static readonly Color CardBorderColor = Color.FromArgb(214, 224, 236);
    private static readonly Color DividerColor = Color.FromArgb(229, 235, 244);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);
    private static readonly Color AccentHoverColor = Color.FromArgb(59, 130, 246);
    private static readonly Color AccentPressedColor = Color.FromArgb(29, 78, 216);
    private static readonly Color AccentSoftColor = Color.FromArgb(232, 241, 255);
    private static readonly Color PrimaryTextColor = Color.FromArgb(15, 23, 42);
    private static readonly Color SecondaryTextColor = Color.FromArgb(100, 116, 139);
    private static readonly Color MutedBackColor = Color.FromArgb(248, 250, 252);
    private static readonly Color InputBackColor = Color.FromArgb(250, 252, 255);

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
    private readonly Button _managementNavButton = new();
    private readonly Button _aboutNavButton = new();
    private readonly Panel _pageHost = new();
    private readonly Icon _icon;
    private Control? _managementPage;
    private Control? _aboutPage;

    public ManagementForm(SettingsStore settingsStore, OverlayController overlayController, Icon icon)
    {
        _settingsStore = settingsStore;
        _overlayController = overlayController;
        _icon = icon;

        AutoScaleDimensions = new SizeF(9F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = PageBackColor;
        ClientSize = new Size(1120, 780);
        DoubleBuffered = true;
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        Icon = icon;
        MinimumSize = new Size(980, 700);
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
        var root = new TableLayoutPanel
        {
            BackColor = PageBackColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildNavigation(), 0, 0);

        _pageHost.BackColor = PageBackColor;
        _pageHost.Dock = DockStyle.Fill;
        root.Controls.Add(_pageHost, 0, 1);

        Controls.Add(root);

        _managementPage = BuildManagementPage();
        _aboutPage = new AboutPanel(_icon)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(20, 12, 20, 20)
        };

        ShowPage(NavigationPage.Management);
    }

    private Control BuildNavigation()
    {
        var navBar = new NavigationBarPanel
        {
            BackColor = Color.White,
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 15, 24, 13)
        };

        var navLayout = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        ConfigureNavButton(_managementNavButton, "管理");
        _managementNavButton.Click += (_, _) => ShowPage(NavigationPage.Management);
        navLayout.Controls.Add(_managementNavButton);

        ConfigureNavButton(_aboutNavButton, "关于");
        _aboutNavButton.Click += (_, _) => ShowPage(NavigationPage.About);
        navLayout.Controls.Add(_aboutNavButton);

        navBar.Controls.Add(navLayout);
        return navBar;
    }

    private void ShowPage(NavigationPage page)
    {
        _pageHost.Controls.Clear();

        var selectedPage = page == NavigationPage.Management ? _managementPage : _aboutPage;
        if (selectedPage is not null)
        {
            _pageHost.Controls.Add(selectedPage);
        }

        UpdateNavButtonStyle(_managementNavButton, page == NavigationPage.Management);
        UpdateNavButtonStyle(_aboutNavButton, page == NavigationPage.About);
    }

    private Control BuildManagementPage()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 20),
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

        var displayCard = CreateCard("显示器", BuildDisplayGrid(), CreateRefreshButton());
        displayCard.Margin = new Padding(0, 0, 0, 14);
        root.Controls.Add(displayCard, 0, 0);

        var settingsCard = CreateCard("设置", BuildSettingsPanel());
        settingsCard.Margin = new Padding(0, 0, 0, 14);
        root.Controls.Add(settingsCard, 0, 1);

        var hotKeyCard = CreateCard("快捷键", BuildHotKeyPanel());
        hotKeyCard.Margin = new Padding(0, 0, 0, 12);
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
        _displayGrid.BorderStyle = BorderStyle.None;
        _displayGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _displayGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _displayGrid.ColumnHeadersHeight = 46;
        _displayGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _displayGrid.Dock = DockStyle.Fill;
        _displayGrid.EditMode = DataGridViewEditMode.EditOnEnter;
        _displayGrid.EnableHeadersVisualStyles = false;
        _displayGrid.GridColor = DividerColor;
        _displayGrid.MultiSelect = false;
        _displayGrid.RowHeadersVisible = false;
        _displayGrid.RowTemplate.Height = 52;
        _displayGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _displayGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            BackColor = MutedBackColor,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(0, 0, 0, 1),
            SelectionBackColor = MutedBackColor,
            SelectionForeColor = Color.FromArgb(30, 41, 59)
        };

        _displayGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = Color.White,
            Font = Font,
            ForeColor = Color.FromArgb(31, 41, 55),
            Padding = new Padding(10, 0, 10, 0),
            SelectionBackColor = AccentSoftColor,
            SelectionForeColor = Color.FromArgb(31, 41, 55)
        };

        var displayColumn = new DataGridViewColumn(new DisplayCheckBoxCell())
        {
            FillWeight = 48,
            HeaderText = "显示器",
            MinimumWidth = 240,
            Name = "Display",
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var typeColumn = CreateTextColumn("Type", "类型", 24);
        var resolutionColumn = CreateTextColumn("Resolution", "分辨率", 28);

        typeColumn.DefaultCellStyle = new DataGridViewCellStyle(_displayGrid.DefaultCellStyle)
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter
        };
        resolutionColumn.DefaultCellStyle = new DataGridViewCellStyle(_displayGrid.DefaultCellStyle)
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter
        };

        _displayGrid.Columns.AddRange(displayColumn, typeColumn, resolutionColumn);
        _displayGrid.CurrentCellDirtyStateChanged += DisplayGrid_CurrentCellDirtyStateChanged;
        _displayGrid.CellMouseUp += DisplayGrid_CellMouseUp;
        return _displayGrid;
    }

    private Control BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 5,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var delayLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0, 6, 12, 0),
            Text = "延时启动",
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(delayLabel, 0, 0);

        _delayInput.Anchor = AnchorStyles.Left;
        _delayInput.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        _delayInput.Maximum = 3600;
        _delayInput.Minimum = 0;
        _delayInput.Size = new Size(104, 34);
        _delayInput.TextAlign = HorizontalAlignment.Right;
        panel.Controls.Add(_delayInput, 1, 0);

        var secondLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = SecondaryTextColor,
            Margin = new Padding(4, 6, 0, 0),
            Text = "秒",
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(secondLabel, 2, 0);

        _brightnessCheckBox.Anchor = AnchorStyles.Left;
        _brightnessCheckBox.AutoSize = true;
        _brightnessCheckBox.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        _brightnessCheckBox.Margin = new Padding(16, 0, 0, 0);
        _brightnessCheckBox.Text = "同时降低硬件亮度";
        panel.Controls.Add(_brightnessCheckBox, 3, 0);
        panel.SetColumnSpan(_brightnessCheckBox, 2);

        var optionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 18, 0, 0),
            WrapContents = true
        };

        _startupCheckBox.AutoSize = true;
        _startupCheckBox.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        _startupCheckBox.Margin = new Padding(0, 0, 30, 0);
        _startupCheckBox.Text = "开机自启动";
        optionPanel.Controls.Add(_startupCheckBox);

        _exitOnMouseMoveCheckBox.AutoSize = true;
        _exitOnMouseMoveCheckBox.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        _exitOnMouseMoveCheckBox.Margin = new Padding(0);
        _exitOnMouseMoveCheckBox.Text = "鼠标移动时退出遮罩";
        optionPanel.Controls.Add(_exitOnMouseMoveCheckBox);

        panel.Controls.Add(optionPanel, 0, 1);
        panel.SetColumnSpan(optionPanel, 5);
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
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        AddHotKeyRow(panel, 0, "切换黑屏", _toggleHotKeyInput, "默认 Ctrl+Alt+B");
        AddHotKeyRow(panel, 1, "快速定时菜单", _quickDelayHotKeyInput, "默认 Ctrl+Alt+T");
        return panel;
    }

    private Control BuildBottomButtons()
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 5,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 0),
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigurePrimaryButton(_startShadeButton, "启动遮罩", 168);
        _startShadeButton.Click += (_, _) => StartShade();
        _startShadeButton.Margin = new Padding(0);
        layout.Controls.Add(_startShadeButton, 1, 0);

        ConfigureSecondaryButton(_delayShadeButton, "延时启动遮罩", 166);
        _delayShadeButton.Click += (_, _) => StartDelayedShade();
        _delayShadeButton.Margin = new Padding(12, 0, 0, 0);
        layout.Controls.Add(_delayShadeButton, 2, 0);

        var saveButton = CreateSecondaryButton("保存", (_, _) => SaveSettings(), 112);
        saveButton.Margin = new Padding(12, 0, 0, 0);
        layout.Controls.Add(saveButton, 3, 0);

        var closeButton = CreateSecondaryButton("关闭", (_, _) => Close(), 112);
        closeButton.Margin = new Padding(12, 0, 0, 0);
        layout.Controls.Add(closeButton, 4, 0);

        return layout;
    }

    private Control CreateCard(string title, Control body, Control? headerAction = null)
    {
        var card = new RoundedPanel
        {
            BorderColor = CardBorderColor,
            CornerRadius = 12,
            Dock = DockStyle.Fill,
            FillColor = CardBackColor,
            Padding = new Padding(28, 20, 28, 24)
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
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold, GraphicsUnit.Point),
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
        var button = CreateSecondaryButton("刷新", (_, _) => RefreshDisplayList(), 112);
        button.Image = CreateRefreshIcon(PrimaryTextColor);
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(10, 0, 10, 0);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        return button;
    }

    private static void ConfigurePrimaryButton(Button button, string text, int width)
    {
        button.AutoSize = false;
        button.BackColor = AccentColor;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderColor = AccentColor;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseDownBackColor = AccentPressedColor;
        button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        button.ForeColor = Color.White;
        button.MinimumSize = new Size(width, 42);
        button.Size = new Size(width, 42);
        button.Text = text;
        button.UseVisualStyleBackColor = false;
        ApplyRoundedRegion(button, 8);
    }

    private static void ConfigureSecondaryButton(Button button, string text, int width)
    {
        button.AutoSize = false;
        button.BackColor = Color.White;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderColor = CardBorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 245, 249);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 252);
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        button.ForeColor = Color.FromArgb(31, 41, 55);
        button.MinimumSize = new Size(width, 42);
        button.Size = new Size(width, 42);
        button.Text = text;
        button.UseVisualStyleBackColor = false;
        ApplyRoundedRegion(button, 8);
    }

    private static void ConfigureNavButton(Button button, string text)
    {
        button.AutoSize = false;
        button.Cursor = Cursors.Hand;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        button.Margin = new Padding(0, 0, 10, 0);
        button.MinimumSize = new Size(102, 40);
        button.Size = new Size(102, 40);
        button.Text = text;
        button.UseVisualStyleBackColor = false;
        ApplyRoundedRegion(button, 8);
    }

    private static void UpdateNavButtonStyle(Button button, bool isSelected)
    {
        button.BackColor = isSelected ? AccentSoftColor : Color.White;
        button.FlatAppearance.BorderColor = isSelected ? Color.FromArgb(174, 204, 255) : Color.White;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseDownBackColor = isSelected ? Color.FromArgb(219, 234, 254) : MutedBackColor;
        button.FlatAppearance.MouseOverBackColor = isSelected ? Color.FromArgb(224, 237, 255) : MutedBackColor;
        button.Font = new Font(button.Font.FontFamily, 10.5F, isSelected ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        button.ForeColor = isSelected ? AccentColor : PrimaryTextColor;
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

        graphics.DrawArc(pen, 3, 3, 12, 12, 35, 300);
        using var brush = new SolidBrush(color);
        var arrow = new[]
        {
            new PointF(13.2F, 1.8F),
            new PointF(16.4F, 3.7F),
            new PointF(12.8F, 5.6F)
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
        foreach (var screen in screens)
        {
            var item = new DisplayItem(screen);
            item.IsSelected = checkedDisplayDeviceNames.Count == 0 || checkedDisplayDeviceNames.Contains(item.DeviceName);
            var rowIndex = _displayGrid.Rows.Add(item.DisplayName, item.DisplayType, item.Resolution);
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
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0, 7, 20, 0),
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, row);

        inputBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        inputBox.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        inputBox.Margin = new Padding(0, 2, 24, 0);
        inputBox.MinimumSize = new Size(320, 42);
        inputBox.Size = new Size(340, 42);
        panel.Controls.Add(inputBox, 1, row);

        var hint = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = SecondaryTextColor,
            Margin = new Padding(0, 7, 0, 0),
            Text = hintText,
            TextAlign = ContentAlignment.MiddleLeft
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
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            const int glyphSize = 18;
            var checkboxBounds = new Rectangle(
                cellBounds.Left + 18,
                cellBounds.Top + (cellBounds.Height - glyphSize) / 2,
                glyphSize,
                glyphSize);

            using (var path = CreateRoundedRectanglePath(checkboxBounds, 4))
            {
                using var fillBrush = new SolidBrush(checkedState ? AccentColor : Color.White);
                using var borderPen = new Pen(checkedState ? AccentColor : CardBorderColor, 1.4F);
                graphics.FillPath(fillBrush, path);
                graphics.DrawPath(borderPen, path);
            }

            if (checkedState)
            {
                using var checkPen = new Pen(Color.White, 2.1F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                graphics.DrawLines(
                    checkPen,
                    [
                        new PointF(checkboxBounds.Left + 4.2F, checkboxBounds.Top + 9.3F),
                        new PointF(checkboxBounds.Left + 7.4F, checkboxBounds.Top + 12.2F),
                        new PointF(checkboxBounds.Left + 13.8F, checkboxBounds.Top + 5.8F)
                    ]);
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

    private sealed class NavigationBarPanel : Panel
    {
        public NavigationBarPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(DividerColor);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
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

    private sealed class DisplayItem(Screen screen)
    {
        public bool IsSelected { get; set; }

        public string DeviceName { get; } = screen.DeviceName;

        public string DisplayName { get; } = DisplayNameResolver.GetDisplayName(screen);

        public string DisplayType { get; } = screen.Primary ? "主显示器" : "扩展显示器";

        public string Resolution { get; } = $"{screen.Bounds.Width}x{screen.Bounds.Height}";
    }

    private static class DisplayNameResolver
    {
        private const int DisplayDeviceActive = 0x00000001;
        private const int ErrorSuccess = 0;
        private const int ErrorInsufficientBuffer = 122;
        private const uint QdcOnlyActivePaths = 0x00000002;
        private const uint DisplayConfigDeviceInfoGetSourceName = 1;
        private const uint DisplayConfigDeviceInfoGetTargetName = 2;

        public static string GetDisplayName(Screen screen)
        {
            var displayName = GetWmiMonitorNameByResolution(screen.Bounds.Size);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            displayName = GetActiveDisplayConfigName(screen.DeviceName);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            displayName = GetActiveMonitorDevice(screen.DeviceName)?.DeviceString?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return NormalizeDeviceName(screen.DeviceName);
        }

        private static string GetWmiMonitorNameByResolution(Size screenSize)
        {
            var monitors = GetWmiMonitors();
            var matchingMonitors = monitors
                .Where(monitor => monitor.SupportsResolution(screenSize))
                .ToArray();

            var monitor = matchingMonitors.Length == 1
                ? matchingMonitors[0]
                : matchingMonitors.FirstOrDefault(monitor => monitor.IsInternal);

            return monitor?.FriendlyName ?? string.Empty;
        }

        private static IReadOnlyList<MonitorDescriptor> GetWmiMonitors()
        {
            if (!OperatingSystem.IsWindows())
            {
                return [];
            }

            try
            {
                var monitorsByInstanceName = new Dictionary<string, MonitorDescriptor>(StringComparer.OrdinalIgnoreCase);

                using (var idSearcher = new ManagementObjectSearcher(
                    @"root\WMI",
                    "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID"))
                using (var idObjects = idSearcher.Get())
                {
                    foreach (ManagementObject idObject in idObjects)
                    {
                        using (idObject)
                        {
                            var instanceName = idObject["InstanceName"] as string;
                            if (string.IsNullOrWhiteSpace(instanceName))
                            {
                                continue;
                            }

                            monitorsByInstanceName[instanceName] = new MonitorDescriptor(
                                instanceName,
                                DecodeMonitorName(idObject["UserFriendlyName"] as ushort[]));
                        }
                    }
                }

                using (var connectionSearcher = new ManagementObjectSearcher(
                    @"root\WMI",
                    "SELECT InstanceName, VideoOutputTechnology FROM WmiMonitorConnectionParams"))
                using (var connectionObjects = connectionSearcher.Get())
                {
                    foreach (ManagementObject connectionObject in connectionObjects)
                    {
                        using (connectionObject)
                        {
                            var monitor = FindWmiMonitor(
                                monitorsByInstanceName,
                                connectionObject["InstanceName"] as string);
                            if (monitor is not null)
                            {
                                monitor.OutputTechnology = Convert.ToUInt32(connectionObject["VideoOutputTechnology"]);
                            }
                        }
                    }
                }

                using (var modesSearcher = new ManagementObjectSearcher(
                    @"root\WMI",
                    "SELECT InstanceName, MonitorSourceModes FROM WmiMonitorListedSupportedSourceModes"))
                using (var modesObjects = modesSearcher.Get())
                {
                    foreach (ManagementObject modesObject in modesObjects)
                    {
                        using (modesObject)
                        {
                            var monitor = FindWmiMonitor(
                                monitorsByInstanceName,
                                modesObject["InstanceName"] as string);
                            if (monitor is null)
                            {
                                continue;
                            }

                            foreach (var mode in (modesObject["MonitorSourceModes"] as Array) ?? Array.Empty<object>())
                            {
                                if (mode is ManagementBaseObject modeObject)
                                {
                                    monitor.SupportedResolutions.Add(new Size(
                                        Convert.ToInt32(modeObject["HorizontalActivePixels"]),
                                        Convert.ToInt32(modeObject["VerticalActivePixels"])));
                                }
                            }
                        }
                    }
                }

                return monitorsByInstanceName.Values
                    .Where(monitor => !string.IsNullOrWhiteSpace(monitor.FriendlyName))
                    .ToArray();
            }
            catch (ManagementException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }

            return [];
        }

        private static MonitorDescriptor? FindWmiMonitor(
            Dictionary<string, MonitorDescriptor> monitorsByInstanceName,
            string? instanceName)
        {
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                return null;
            }

            return monitorsByInstanceName.TryGetValue(instanceName, out var monitor)
                ? monitor
                : null;
        }

        private static string DecodeMonitorName(ushort[]? rawName)
        {
            if (rawName is null)
            {
                return string.Empty;
            }

            var chars = rawName
                .TakeWhile(value => value > 0)
                .Select(value => (char)value)
                .ToArray();

            return new string(chars).Trim();
        }

        private static string GetActiveDisplayConfigName(string displayDeviceName)
        {
            if (!OperatingSystem.IsWindows())
            {
                return string.Empty;
            }

            foreach (var path in GetActiveDisplayPaths())
            {
                var sourceName = GetSourceDeviceName(path.SourceInfo.AdapterId, path.SourceInfo.Id);
                if (!displayDeviceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetName = GetTargetDeviceName(path.TargetInfo.AdapterId, path.TargetInfo.Id);
                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    return targetName;
                }
            }

            return string.Empty;
        }

        private static IReadOnlyList<DisplayConfigPathInfo> GetActiveDisplayPaths()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var result = NativeMethods.GetDisplayConfigBufferSizes(
                    QdcOnlyActivePaths,
                    out var pathCount,
                    out var modeCount);

                if (result != ErrorSuccess || pathCount == 0)
                {
                    return [];
                }

                var paths = new DisplayConfigPathInfo[pathCount];
                var modes = new DisplayConfigModeInfo[modeCount];
                result = NativeMethods.QueryDisplayConfig(
                    QdcOnlyActivePaths,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero);

                if (result == ErrorSuccess)
                {
                    return paths.Take((int)pathCount).ToArray();
                }

                if (result != ErrorInsufficientBuffer)
                {
                    return [];
                }
            }

            return [];
        }

        private static string GetSourceDeviceName(DisplayConfigLuid adapterId, uint sourceId)
        {
            var sourceName = new DisplayConfigSourceDeviceName
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DisplayConfigDeviceInfoGetSourceName,
                    Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                    AdapterId = adapterId,
                    Id = sourceId
                }
            };

            return NativeMethods.DisplayConfigGetDeviceInfo(ref sourceName) == ErrorSuccess
                ? sourceName.ViewGdiDeviceName.TrimEnd('\0').Trim()
                : string.Empty;
        }

        private static string GetTargetDeviceName(DisplayConfigLuid adapterId, uint targetId)
        {
            var targetName = new DisplayConfigTargetDeviceName
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DisplayConfigDeviceInfoGetTargetName,
                    Size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                    AdapterId = adapterId,
                    Id = targetId
                }
            };

            if (NativeMethods.DisplayConfigGetDeviceInfo(ref targetName) != ErrorSuccess)
            {
                return string.Empty;
            }

            return targetName.MonitorFriendlyDeviceName.TrimEnd('\0').Trim();
        }

        private static DisplayDevice? GetActiveMonitorDevice(string displayDeviceName)
        {
            for (uint deviceIndex = 0; ; deviceIndex++)
            {
                var displayDevice = CreateDisplayDevice();
                if (!NativeMethods.EnumDisplayDevices(displayDeviceName, deviceIndex, ref displayDevice, 0))
                {
                    break;
                }

                if ((displayDevice.StateFlags & DisplayDeviceActive) == 0)
                {
                    continue;
                }

                return displayDevice;
            }

            return null;
        }

        private static string NormalizeDeviceName(string deviceName)
        {
            const string displayPrefix = @"\\.\";
            return deviceName.StartsWith(displayPrefix, StringComparison.Ordinal)
                ? deviceName[displayPrefix.Length..]
                : deviceName;
        }

        private static DisplayDevice CreateDisplayDevice()
        {
            return new DisplayDevice
            {
                cb = Marshal.SizeOf<DisplayDevice>()
            };
        }

        private sealed class MonitorDescriptor(string instanceName, string friendlyName)
        {
            private const uint InternalDisplayTechnology = 11;

            public string InstanceName { get; } = instanceName;

            public string FriendlyName { get; } = friendlyName;

            public uint OutputTechnology { get; set; }

            public HashSet<Size> SupportedResolutions { get; } = [];

            public bool IsInternal => OutputTechnology == InternalDisplayTechnology;

            public bool SupportsResolution(Size resolution)
            {
                return SupportedResolutions.Contains(resolution);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigLuid
        {
            public uint LowPart;

            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathInfo
        {
            public DisplayConfigPathSourceInfo SourceInfo;

            public DisplayConfigPathTargetInfo TargetInfo;

            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathSourceInfo
        {
            public DisplayConfigLuid AdapterId;

            public uint Id;

            public uint ModeInfoIdx;

            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathTargetInfo
        {
            public DisplayConfigLuid AdapterId;

            public uint Id;

            public uint ModeInfoIdx;

            public uint OutputTechnology;

            public uint Rotation;

            public uint Scaling;

            public DisplayConfigRational RefreshRate;

            public uint ScanLineOrdering;

            [MarshalAs(UnmanagedType.Bool)]
            public bool TargetAvailable;

            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigRational
        {
            public uint Numerator;

            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigModeInfo
        {
            public uint InfoType;

            public uint Id;

            public DisplayConfigLuid AdapterId;

            public DisplayConfigTargetMode TargetMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigTargetMode
        {
            public DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigVideoSignalInfo
        {
            public ulong PixelRate;

            public DisplayConfigRational HSyncFreq;

            public DisplayConfigRational VSyncFreq;

            public DisplayConfig2DRegion ActiveSize;

            public DisplayConfig2DRegion TotalSize;

            public uint VideoStandard;

            public uint ScanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfig2DRegion
        {
            public uint Width;

            public uint Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigDeviceInfoHeader
        {
            public uint Type;

            public uint Size;

            public DisplayConfigLuid AdapterId;

            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayConfigSourceDeviceName
        {
            public DisplayConfigDeviceInfoHeader Header;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ViewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayConfigTargetDeviceName
        {
            public DisplayConfigDeviceInfoHeader Header;

            public uint Flags;

            public uint OutputTechnology;

            public ushort EdidManufactureId;

            public ushort EdidProductCodeId;

            public uint ConnectorInstance;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string MonitorFriendlyDeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string MonitorDevicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            public int cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            public static extern int GetDisplayConfigBufferSizes(
                uint flags,
                out uint numPathArrayElements,
                out uint numModeInfoArrayElements);

            [DllImport("user32.dll")]
            public static extern int QueryDisplayConfig(
                uint flags,
                ref uint numPathArrayElements,
                [Out] DisplayConfigPathInfo[] pathArray,
                ref uint numModeInfoArrayElements,
                [Out] DisplayConfigModeInfo[] modeInfoArray,
                IntPtr currentTopologyId);

            [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
            public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

            [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
            public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumDisplayDevices(
                string? lpDevice,
                uint iDevNum,
                ref DisplayDevice lpDisplayDevice,
                uint dwFlags);
        }
    }

    private enum NavigationPage
    {
        Management,
        About
    }
}
