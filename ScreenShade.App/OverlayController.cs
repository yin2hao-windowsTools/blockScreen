using ScreenShade.App.Brightness;

namespace ScreenShade.App;

internal sealed class OverlayController : IDisposable
{
    private readonly BrightnessController _brightnessController = new();
    private readonly List<OverlayForm> _forms = [];
    private CancellationTokenSource? _brightnessCancellation;
    private Task _brightnessTask = Task.CompletedTask;
    private bool _isActive;

    public bool IsActive => _isActive;

    public void ShowShade()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        _brightnessCancellation = new CancellationTokenSource();

        foreach (var screen in Screen.AllScreens)
        {
            var form = new OverlayForm(screen.Bounds, HideShade);
            _forms.Add(form);
            form.Show();
        }

        var cancellationToken = _brightnessCancellation.Token;
        _brightnessTask = Task.Run(async () =>
        {
            try
            {
                await _brightnessController.DimAsync(cancellationToken).ConfigureAwait(false);
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

    public void HideShade()
    {
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
    }

    public void ToggleShade()
    {
        if (_isActive)
        {
            HideShade();
        }
        else
        {
            ShowShade();
        }
    }

    public void Dispose()
    {
        HideShade();
        _brightnessTask.GetAwaiter().GetResult();
        _brightnessController.Dispose();
    }
}
