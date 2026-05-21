using Microsoft.Win32;

namespace ScreenShade.App;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "A1 Screen Shade";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var command = key?.GetValue(ValueName) as string;
        return PointsToCurrentExecutable(command);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            Enable();
            return;
        }

        Disable();
    }

    private static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("无法打开 Windows 启动项注册表。");

        key.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
    }

    private static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static bool PointsToCurrentExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var normalizedCommand = command.Trim();
        return string.Equals(normalizedCommand, BuildCommand(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand.Trim('"'), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommand()
    {
        return $"\"{Application.ExecutablePath}\"";
    }
}
