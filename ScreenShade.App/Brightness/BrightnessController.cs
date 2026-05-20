namespace ScreenShade.App.Brightness;

internal sealed class BrightnessController : IDisposable
{
    private readonly IBrightnessProvider _physicalMonitorProvider = new PhysicalMonitorBrightnessProvider();
    private readonly IBrightnessProvider _wmiProvider = new WmiBrightnessProvider();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<IBrightnessRestorePoint>? _restorePoints;

    public async Task DimAsync(IReadOnlyCollection<string> displayDeviceNames, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var restorePoints = new List<IBrightnessRestorePoint>();

        try
        {
            if (_restorePoints is not null)
            {
                return;
            }

            var targetDisplayDeviceNames = ScreenShadeSettings.NormalizeDisplayDeviceNames(displayDeviceNames);
            var allDisplaysSelected = Screen.AllScreens.All(screen => targetDisplayDeviceNames.Contains(screen.DeviceName));
            if (targetDisplayDeviceNames.Count == 0 || allDisplaysSelected)
            {
                TryDim(_wmiProvider, restorePoints, null);
            }

            if (restorePoints.Count == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDim(
                    _physicalMonitorProvider,
                    restorePoints,
                    targetDisplayDeviceNames.Count == 0 || allDisplaysSelected ? null : targetDisplayDeviceNames);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _restorePoints = restorePoints;
            restorePoints = [];
        }
        catch (OperationCanceledException)
        {
            RestorePoints(restorePoints);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IBrightnessRestorePoint>? restorePoints;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            restorePoints = _restorePoints;
            _restorePoints = null;
        }
        finally
        {
            _lock.Release();
        }

        if (restorePoints is null)
        {
            return;
        }

        foreach (var restorePoint in restorePoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestorePoint(restorePoint);
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    private static void RestorePoints(IEnumerable<IBrightnessRestorePoint> restorePoints)
    {
        foreach (var restorePoint in restorePoints)
        {
            RestorePoint(restorePoint);
        }
    }

    private static void RestorePoint(IBrightnessRestorePoint restorePoint)
    {
        try
        {
            restorePoint.Restore();
        }
        catch
        {
            // Restoration is best effort because displays can be disconnected or reject DDC commands.
        }
    }

    private static void TryDim(
        IBrightnessProvider provider,
        ICollection<IBrightnessRestorePoint> restorePoints,
        IReadOnlySet<string>? displayDeviceNames)
    {
        try
        {
            foreach (var restorePoint in provider.DimToMinimum(displayDeviceNames))
            {
                restorePoints.Add(restorePoint);
            }
        }
        catch
        {
            // Brightness APIs are device-dependent. A failed provider should not block the overlay.
        }
    }
}
