using System.Text.Json;

namespace ScreenShade.App;

internal sealed class ScreenShadeSettings
{
    public List<string> DisplayDeviceNames { get; set; } = [];

    public int DelaySeconds { get; set; }

    public bool DimHardwareBrightness { get; set; } = true;

    public bool ExitOnMouseMove { get; set; }

    public HotKeySettings ToggleShadeHotKey { get; set; } = HotKeySettings.DefaultToggle();

    public HotKeySettings QuickDelayHotKey { get; set; } = HotKeySettings.DefaultDelayMenu();

    public ScreenShadeSettings Clone()
    {
        var toggleShadeHotKey = ToggleShadeHotKey?.Normalize(HotKeySettings.DefaultToggle())
            ?? HotKeySettings.DefaultToggle();
        var quickDelayHotKey = QuickDelayHotKey?.Normalize(HotKeySettings.DefaultDelayMenu())
            ?? HotKeySettings.DefaultDelayMenu();

        if (toggleShadeHotKey.HasSameGesture(quickDelayHotKey))
        {
            quickDelayHotKey = HotKeySettings.DefaultDelayMenu();
        }

        return new ScreenShadeSettings
        {
            DisplayDeviceNames = [.. NormalizeDisplayDeviceNames(DisplayDeviceNames)],
            DelaySeconds = Math.Clamp(DelaySeconds, 0, 3600),
            DimHardwareBrightness = DimHardwareBrightness,
            ExitOnMouseMove = ExitOnMouseMove,
            ToggleShadeHotKey = toggleShadeHotKey,
            QuickDelayHotKey = quickDelayHotKey
        };
    }

    public IReadOnlyList<Screen> ResolveScreens()
    {
        var screens = Screen.AllScreens;
        var selectedDisplayDeviceNames = NormalizeDisplayDeviceNames(DisplayDeviceNames);
        if (selectedDisplayDeviceNames.Count == 0)
        {
            return screens;
        }

        var selectedScreens = screens
            .Where(screen => selectedDisplayDeviceNames.Contains(screen.DeviceName))
            .ToArray();

        return selectedScreens.Length > 0 ? selectedScreens : screens;
    }

    public bool IsSelected(Screen screen)
    {
        var selectedDisplayDeviceNames = NormalizeDisplayDeviceNames(DisplayDeviceNames);
        return selectedDisplayDeviceNames.Count == 0 || selectedDisplayDeviceNames.Contains(screen.DeviceName);
    }

    public static HashSet<string> NormalizeDisplayDeviceNames(IEnumerable<string>? displayDeviceNames)
    {
        return displayDeviceNames?
            .Where(displayDeviceName => !string.IsNullOrWhiteSpace(displayDeviceName))
            .Select(displayDeviceName => displayDeviceName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }
}

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "A1 Screen Shade");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    public SettingsStore()
    {
        Settings = Load();
    }

    public ScreenShadeSettings Settings { get; private set; }

    public event EventHandler? SettingsChanged;

    public void Save(ScreenShadeSettings settings)
    {
        var normalizedSettings = settings.Clone();
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(normalizedSettings, SerializerOptions));
        Settings = normalizedSettings;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ScreenShadeSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new ScreenShadeSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<ScreenShadeSettings>(json, SerializerOptions);
            return settings?.Clone() ?? new ScreenShadeSettings();
        }
        catch
        {
            return new ScreenShadeSettings();
        }
    }
}
