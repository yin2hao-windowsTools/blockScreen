using System.Runtime.InteropServices;

namespace ScreenShade.App;

internal sealed class OverlayForm : Form
{
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly Action _dismiss;
    private readonly bool _exitOnMouseMove;
    private readonly HashSet<Keys> _keysDownOnOpen = CapturePressedKeys();
    private readonly Point _initialMousePosition;

    public OverlayForm(Rectangle bounds, Action dismiss, bool exitOnMouseMove)
    {
        _dismiss = dismiss;
        _exitOnMouseMove = exitOnMouseMove;
        _initialMousePosition = Cursor.Position;

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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_exitOnMouseMove && Cursor.Position != _initialMousePosition)
        {
            _dismiss();
            return;
        }

        base.OnMouseMove(e);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmKeyDown:
            case WmSysKeyDown:
            {
                var key = (Keys)m.WParam.ToInt32();
                // Ignore the launch hotkey until its keys are released.
                if (_keysDownOnOpen.Remove(key))
                {
                    break;
                }

                _dismiss();
                return;
            }
            case WmKeyUp:
            case WmSysKeyUp:
                _keysDownOnOpen.Remove((Keys)m.WParam.ToInt32());
                break;
        }

        base.WndProc(ref m);
    }

    private static HashSet<Keys> CapturePressedKeys()
    {
        var pressedKeys = new HashSet<Keys>();
        for (var virtualKey = 1; virtualKey <= 0xFF; virtualKey++)
        {
            if ((NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0)
            {
                pressedKeys.Add((Keys)virtualKey);
            }
        }

        return pressedKeys;
    }

    private static partial class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int vKey);
    }
}
