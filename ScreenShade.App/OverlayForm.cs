namespace ScreenShade.App;

internal sealed class OverlayForm : Form
{
    private readonly Action _dismiss;

    public OverlayForm(Rectangle bounds, Action dismiss)
    {
        _dismiss = dismiss;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        Bounds = bounds;
        Cursor = Cursors.Hand;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        WindowState = FormWindowState.Normal;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return createParams;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        _dismiss();
        base.OnClick(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _dismiss();
        base.OnMouseDown(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is Keys.Escape)
        {
            _dismiss();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
