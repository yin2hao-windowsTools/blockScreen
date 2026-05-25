namespace ScreenShade.App;

internal sealed class AboutForm : Form
{
    private readonly Button _checkUpdateButton = new();
    private readonly Label _updateStatusLabel = new();
    private readonly Bitmap _iconBitmap;

    public AboutForm(Icon icon)
    {
        _iconBitmap = icon.ToBitmap();

        AutoScaleDimensions = new SizeF(9F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(560, 430);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Icon = icon;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = $"关于 {AppInfo.Name}";

        BuildLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconBitmap.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildInfoPanel(), 0, 1);
        root.Controls.Add(BuildLicenseGroup(), 0, 2);

        _updateStatusLabel.AutoEllipsis = true;
        _updateStatusLabel.Dock = DockStyle.Fill;
        _updateStatusLabel.ForeColor = SystemColors.GrayText;
        _updateStatusLabel.Text = "可检查 GitHub Release 是否有新版本。";
        _updateStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_updateStatusLabel, 0, 3);

        root.Controls.Add(BuildButtonPanel(), 0, 4);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Top,
            Image = _iconBitmap,
            Margin = new Padding(0, 2, 18, 0),
            SizeMode = PictureBoxSizeMode.CenterImage
        };
        panel.Controls.Add(iconBox, 0, 0);
        panel.SetRowSpan(iconBox, 2);

        var titleLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold, GraphicsUnit.Point),
            Text = AppInfo.Name,
            TextAlign = ContentAlignment.BottomLeft
        };
        panel.Controls.Add(titleLabel, 1, 0);

        var versionLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = $"版本 {AppInfo.CurrentVersion}",
            TextAlign = ContentAlignment.TopLeft
        };
        panel.Controls.Add(versionLabel, 1, 1);

        return panel;
    }

    private Control BuildInfoPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        AddInfoRow(panel, 0, "开发者", CreateLink(AppInfo.DeveloperName, AppInfo.DeveloperHomeUrl));
        AddInfoRow(panel, 1, "项目主页", CreateLink(AppInfo.RepositoryUrl, AppInfo.RepositoryUrl));
        AddInfoRow(panel, 2, "许可证", new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Text = AppInfo.LicenseName,
            TextAlign = ContentAlignment.MiddleLeft
        });

        return panel;
    }

    private Control BuildLicenseGroup()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "许可证"
        };

        var licenseText = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            Text = AppInfo.LicenseDescription,
            BackColor = SystemColors.Control,
            ForeColor = SystemColors.ControlText,
            Margin = new Padding(12)
        };

        group.Controls.Add(licenseText);
        return group;
    }

    private Control BuildButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var closeButton = new Button
        {
            MinimumSize = new Size(92, 38),
            Text = "关闭",
            UseVisualStyleBackColor = true
        };
        closeButton.Click += (_, _) => Close();
        panel.Controls.Add(closeButton);

        _checkUpdateButton.MinimumSize = new Size(112, 38);
        _checkUpdateButton.Text = "检查更新";
        _checkUpdateButton.UseVisualStyleBackColor = true;
        _checkUpdateButton.Click += CheckUpdateButton_Click;
        panel.Controls.Add(_checkUpdateButton);

        return panel;
    }

    private static void AddInfoRow(TableLayoutPanel panel, int row, string labelText, Control valueControl)
    {
        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, row);

        valueControl.Dock = DockStyle.Fill;
        panel.Controls.Add(valueControl, 1, row);
    }

    private static LinkLabel CreateLink(string text, string url)
    {
        var link = new LinkLabel
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
        link.Links.Add(0, text.Length, url);
        link.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string linkUrl)
            {
                ExternalLink.Open(linkUrl);
            }
        };
        return link;
    }

    private async void CheckUpdateButton_Click(object? sender, EventArgs e)
    {
        _checkUpdateButton.Enabled = false;
        _updateStatusLabel.Text = "正在检查 GitHub Release...";

        try
        {
            var result = await UpdateChecker.CheckLatestReleaseAsync();
            _updateStatusLabel.Text = UpdateCheckDialog.GetStatusText(result);
            UpdateCheckDialog.ShowResult(this, result);
        }
        catch (Exception ex)
        {
            _updateStatusLabel.Text = "检查更新失败。";
            MessageBox.Show(this, $"检查更新失败：{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _checkUpdateButton.Enabled = true;
        }
    }
}
