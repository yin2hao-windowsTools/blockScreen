using System.Runtime.InteropServices;

namespace ScreenShade.App;

internal sealed class GlobalHotKeyWindow : NativeWindow, IDisposable
{
    private const int HotKeyId = 100;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkB = 0x42;

    private readonly Action _onPressed;
    private bool _registered;

    public GlobalHotKeyWindow(Action onPressed)
    {
        _onPressed = onPressed;
        CreateHandle(new CreateParams());
        _registered = NativeMethods.RegisterHotKey(Handle, HotKeyId, ModControl | ModAlt, VkB);
    }

    public bool IsRegistered => _registered;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && m.WParam.ToInt32() == HotKeyId)
        {
            _onPressed();
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(Handle, HotKeyId);
            _registered = false;
        }

        DestroyHandle();
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
