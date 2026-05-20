namespace ScreenShade.App;

internal sealed class HotKeySettings
{
    public bool Control { get; set; } = true;

    public bool Alt { get; set; } = true;

    public bool Shift { get; set; }

    public Keys Key { get; set; } = Keys.B;

    public static HotKeySettings DefaultToggle()
    {
        return new HotKeySettings
        {
            Control = true,
            Alt = true,
            Shift = false,
            Key = Keys.B
        };
    }

    public static HotKeySettings DefaultDelayMenu()
    {
        return new HotKeySettings
        {
            Control = true,
            Alt = true,
            Shift = false,
            Key = Keys.T
        };
    }

    public HotKeySettings Clone()
    {
        return new HotKeySettings
        {
            Control = Control,
            Alt = Alt,
            Shift = Shift,
            Key = Key & Keys.KeyCode
        };
    }

    public HotKeySettings Normalize(HotKeySettings fallback)
    {
        var normalized = Clone();
        return normalized.IsValid ? normalized : fallback.Clone();
    }

    public bool IsValid => Key != Keys.None && !IsModifierKey(Key) && (Control || Alt || Shift);

    public bool HasSameGesture(HotKeySettings other)
    {
        return Control == other.Control
            && Alt == other.Alt
            && Shift == other.Shift
            && (Key & Keys.KeyCode) == (other.Key & Keys.KeyCode);
    }

    public string ToDisplayText()
    {
        if (!IsValid)
        {
            return "未设置";
        }

        var parts = new List<string>(4);
        if (Control)
        {
            parts.Add("Ctrl");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        parts.Add(GetKeyText(Key));
        return string.Join("+", parts);
    }

    public static HotKeySettings FromKeyData(Keys keyData)
    {
        return new HotKeySettings
        {
            Control = (keyData & Keys.Control) == Keys.Control,
            Alt = (keyData & Keys.Alt) == Keys.Alt,
            Shift = (keyData & Keys.Shift) == Keys.Shift,
            Key = keyData & Keys.KeyCode
        };
    }

    private static string GetKeyText(Keys key)
    {
        return key switch
        {
            Keys.D0 => "0",
            Keys.D1 => "1",
            Keys.D2 => "2",
            Keys.D3 => "3",
            Keys.D4 => "4",
            Keys.D5 => "5",
            Keys.D6 => "6",
            Keys.D7 => "7",
            Keys.D8 => "8",
            Keys.D9 => "9",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemQuestion => "/",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.Oemtilde => "`",
            _ => key.ToString()
        };
    }

    private static bool IsModifierKey(Keys key)
    {
        return key is Keys.ControlKey
            or Keys.LControlKey
            or Keys.RControlKey
            or Keys.Menu
            or Keys.LMenu
            or Keys.RMenu
            or Keys.ShiftKey
            or Keys.LShiftKey
            or Keys.RShiftKey;
    }
}
