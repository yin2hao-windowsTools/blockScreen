using System.Runtime.InteropServices;

namespace ScreenShade.App;

internal sealed class GlobalHotKeyWindow : NativeWindow, IDisposable
{
    private const int ToggleShadeHotKeyId = 100;
    private const int QuickDelayHotKeyId = 101;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    private readonly Action _onToggleShade;
    private readonly Action _onQuickDelay;
    private bool _toggleShadeRegistered;
    private bool _quickDelayRegistered;

    public GlobalHotKeyWindow(Action onToggleShade, Action onQuickDelay)
    {
        _onToggleShade = onToggleShade;
        _onQuickDelay = onQuickDelay;
        CreateHandle(new CreateParams());
    }

    public bool IsToggleShadeRegistered => _toggleShadeRegistered;

    public bool IsQuickDelayRegistered => _quickDelayRegistered;

    public void Apply(ScreenShadeSettings settings)
    {
        UnregisterAll();

        var normalizedSettings = settings.Clone();
        _toggleShadeRegistered = Register(ToggleShadeHotKeyId, normalizedSettings.ToggleShadeHotKey);
        _quickDelayRegistered = Register(QuickDelayHotKeyId, normalizedSettings.QuickDelayHotKey);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
            switch (m.WParam.ToInt32())
            {
                case ToggleShadeHotKeyId:
                    _onToggleShade();
                    return;
                case QuickDelayHotKeyId:
                    _onQuickDelay();
                    return;
            }
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }

    private bool Register(int id, HotKeySettings hotKey)
    {
        if (!hotKey.IsValid)
        {
            return false;
        }

        return NativeMethods.RegisterHotKey(Handle, id, ToModifiers(hotKey), (uint)hotKey.Key);
    }

    private void UnregisterAll()
    {
        if (_toggleShadeRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, ToggleShadeHotKeyId);
            _toggleShadeRegistered = false;
        }

        if (_quickDelayRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, QuickDelayHotKeyId);
            _quickDelayRegistered = false;
        }
    }

    private static uint ToModifiers(HotKeySettings hotKey)
    {
        var modifiers = ModNoRepeat;
        if (hotKey.Control)
        {
            modifiers |= ModControl;
        }

        if (hotKey.Alt)
        {
            modifiers |= ModAlt;
        }

        if (hotKey.Shift)
        {
            modifiers |= ModShift;
        }

        return modifiers;
    }

    private static partial class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
