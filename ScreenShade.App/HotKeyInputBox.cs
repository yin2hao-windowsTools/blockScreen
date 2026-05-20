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
        var hotKey = HotKeySettings.FromKeyData(e.KeyData);
        if (hotKey.IsValid)
        {
            HotKey = hotKey;
        }
        else
        {
            Text = "请按 Ctrl/Alt/Shift + 按键";
            SelectAll();
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnEnter(EventArgs e)
    {
        SelectAll();
        base.OnEnter(e);
    }
}
