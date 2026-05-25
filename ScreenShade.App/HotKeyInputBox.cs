namespace ScreenShade.App;

internal sealed class HotKeyInputBox : TextBox
{
    private HotKeySettings _hotKey = HotKeySettings.DefaultToggle();

    public HotKeyInputBox()
    {
        BorderStyle = BorderStyle.FixedSingle;
        ReadOnly = true;
        TabStop = true;
    }

    public HotKeySettings HotKey
    {
        get => _hotKey.Clone();
        set
        {
            _hotKey = value.Clone();
            Text = _hotKey.ToDisplayText();
        }
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
        var hotKey = HotKeySettings.FromKeyData(keyData);
        if (hotKey.IsValid)
        {
            HotKey = hotKey;
        }
        else
        {
            Text = "请按 Ctrl/Alt/Shift + 按键";
            SelectAll();
        }
    }

    private static bool ShouldLetFormHandleKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        var modifiers = keyData & Keys.Modifiers;
        return keyCode == Keys.Tab && (modifiers == Keys.None || modifiers == Keys.Shift);
    }

    protected override void OnEnter(EventArgs e)
    {
        SelectAll();
        base.OnEnter(e);
    }
}
