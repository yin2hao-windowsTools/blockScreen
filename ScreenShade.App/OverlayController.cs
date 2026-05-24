using ScreenShade.App.Brightness;

namespace ScreenShade.App;

internal sealed class OverlayController : IDisposable
{
    private readonly BrightnessController _brightnessController = new();
    private readonly List<OverlayForm> _forms = [];
    private System.Windows.Forms.Timer? _delayTimer;
    private ScreenShadeSettings? _pendingSettings;
    private CancellationTokenSource? _brightnessCancellation;
    private Task _brightnessTask = Task.CompletedTask;
    private bool _isActive;

    public bool IsActive => _isActive;

    public bool IsPending => _delayTimer is not null;

    public event EventHandler? StateChanged;

    public void ShowShade(ScreenShadeSettings settings)
    {
        if (_isActive)
        {
            return;
        }

        CancelPendingShade(false);
        ShowShadeNow(settings.Clone());
    }

    public void ShowShadeWithDelay(ScreenShadeSettings settings)
    {
        if (_isActive)
        {
            return;
        }

        CancelPendingShade(false);

        var settingsSnapshot = settings.Clone();
        if (settingsSnapshot.DelaySeconds > 0)
        {
            _pendingSettings = settingsSnapshot;
            _delayTimer = new System.Windows.Forms.Timer
            {
                Interval = Math.Max(1, settingsSnapshot.DelaySeconds) * 1000
            };
            _delayTimer.Tick += (_, _) =>
            {
                var pendingSettings = _pendingSettings;
                CancelPendingShade(false);
                if (pendingSettings is not null)
                {
                    ShowShadeNow(pendingSettings);
                }
            };
            _delayTimer.Start();
            OnStateChanged();
            return;
        }

        ShowShadeNow(settingsSnapshot);
    }

    private void ShowShadeNow(ScreenShadeSettings settings)
    {
        _isActive = true;
        _brightnessCancellation = new CancellationTokenSource();

        var selectedScreens = settings.ResolveScreens();
        foreach (var screen in selectedScreens)
        {
            var form = new OverlayForm(screen.Bounds, HideShade, settings.ExitOnMouseMove);
            _forms.Add(form);
            form.Show();
        }

        if (settings.DimHardwareBrightness)
        {
            var displayDeviceNames = selectedScreens.Select(screen => screen.DeviceName).ToArray();
            var cancellationToken = _brightnessCancellation.Token;
            _brightnessTask = Task.Run(async () =>
            {
                try
                {
                    await _brightnessController.DimAsync(displayDeviceNames, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    // Brightness changes are best effort; the black overlay is the reliable fallback.
                }
            }, cancellationToken);
        }
        else
        {
            _brightnessTask = Task.CompletedTask;
        }

        OnStateChanged();
    }

    public void HideShade()
    {
        if (IsPending)
        {
            CancelPendingShade();
            return;
        }

        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _brightnessCancellation?.Cancel();
        _brightnessCancellation?.Dispose();
        _brightnessCancellation = null;

        foreach (var form in _forms.ToArray())
        {
            form.Close();
            form.Dispose();
        }

        _forms.Clear();
        _brightnessTask = Task.Run(async () =>
        {
            try
            {
                await _brightnessController.RestoreAsync().ConfigureAwait(false);
            }
            catch
            {
                // Some displays reject restore calls after unplug, sleep, or permission changes.
            }
        });

        OnStateChanged();
    }

    public void ToggleShade(ScreenShadeSettings settings)
    {
        if (_isActive || IsPending)
        {
            HideShade();
        }
        else
        {
            ShowShade(settings);
        }
    }

    public void Dispose()
    {
        CancelPendingShade(false);
        HideShade();
        _brightnessTask.GetAwaiter().GetResult();
        _brightnessController.Dispose();
    }

    private void CancelPendingShade(bool raiseStateChanged = true)
    {
        _delayTimer?.Stop();
        _delayTimer?.Dispose();
        _delayTimer = null;
        _pendingSettings = null;

        if (raiseStateChanged)
        {
            OnStateChanged();
        }
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
