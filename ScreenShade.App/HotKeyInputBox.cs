using System.Drawing.Drawing2D;

namespace ScreenShade.App;

internal sealed class HotKeyInputBox : Control
{
    private static readonly Color DefaultBorderColor = Color.FromArgb(214, 224, 236);
    private static readonly Color DefaultFocusBorderColor = Color.FromArgb(37, 99, 235);

    private HotKeySettings _hotKey = HotKeySettings.DefaultToggle();

    public HotKeyInputBox()
    {
        AccessibleRole = AccessibleRole.Text;
        BackColor = Color.FromArgb(250, 252, 255);
        Cursor = Cursors.IBeam;
        MinimumSize = new Size(240, 40);
        Size = new Size(320, 42);
        ReadOnly = true;
        TabStop = true;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable
            | ControlStyles.UserPaint,
            true);
    }

    public Color BorderColor { get; set; } = DefaultBorderColor;

    public int CornerRadius { get; set; } = 8;

    public Color FocusBorderColor { get; set; } = DefaultFocusBorderColor;

    public bool ReadOnly { get; set; }

    public HotKeySettings HotKey
    {
        get => _hotKey.Clone();
        set
        {
            _hotKey = value.Clone();
            Text = _hotKey.ToDisplayText();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var fillBrush = new SolidBrush(BackColor);
        using var borderPen = new Pen(Focused ? FocusBorderColor : BorderColor, Focused ? 1.6F : 1F);
        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        var textBounds = new Rectangle(12, 0, Math.Max(0, Width - 24), Height);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        CaptureHotKey(e.KeyData);

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (ShouldLetFormHandleKey(keyData))
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        CaptureHotKey(keyData);
        return true;
    }

    private void CaptureHotKey(Keys keyData)
    {
        if (ShouldClearHotKey(keyData))
        {
            HotKey = HotKeySettings.Empty();
            return;
        }

        var hotKey = HotKeySettings.FromKeyData(keyData);
        if (hotKey.IsValid)
        {
            HotKey = hotKey;
        }
        else
        {
            Text = "请按 Ctrl/Alt/Shift + 按键，或按 Del 清空";
            Invalidate();
        }
    }

    private static bool ShouldLetFormHandleKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        var modifiers = keyData & Keys.Modifiers;
        return keyCode == Keys.Tab && (modifiers == Keys.None || modifiers == Keys.Shift);
    }

    private static bool ShouldClearHotKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        var modifiers = keyData & Keys.Modifiers;
        return modifiers == Keys.None
            && keyCode is Keys.Delete or Keys.Back or Keys.Escape;
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Invalidate();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
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
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
