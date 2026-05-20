using System.Management;

namespace ScreenShade.App.Brightness;

internal sealed class WmiBrightnessProvider : IBrightnessProvider
{
    private const string Scope = @"root\WMI";

    public IReadOnlyList<IBrightnessRestorePoint> DimToMinimum(IReadOnlySet<string>? displayDeviceNames)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        if (displayDeviceNames is { Count: > 0 })
        {
            return [];
        }

        using var searcher = new ManagementObjectSearcher(Scope, "SELECT * FROM WmiMonitorBrightness");
        using var brightnessObjects = searcher.Get();
        var restorePoints = new List<IBrightnessRestorePoint>();

        foreach (ManagementObject brightnessObject in brightnessObjects)
        {
            using (brightnessObject)
            {
                var instanceName = brightnessObject["InstanceName"] as string;
                if (string.IsNullOrWhiteSpace(instanceName))
                {
                    continue;
                }

                var currentBrightness = Convert.ToByte(brightnessObject["CurrentBrightness"]);
                if (SetBrightness(instanceName, 0))
                {
                    restorePoints.Add(new WmiBrightnessRestorePoint(instanceName, currentBrightness));
                }
            }
        }

        return restorePoints;
    }

    private static bool SetBrightness(string instanceName, byte brightness)
    {
        var escapedInstanceName = instanceName.Replace("\\", "\\\\", StringComparison.Ordinal);
        using var methods = new ManagementObjectSearcher(
            Scope,
            $"SELECT * FROM WmiMonitorBrightnessMethods WHERE InstanceName = '{escapedInstanceName}'");

        using var methodObjects = methods.Get();
        foreach (ManagementObject methodObject in methodObjects)
        {
            using (methodObject)
            {
                methodObject.InvokeMethod("WmiSetBrightness", [1u, brightness]);
                return true;
            }
        }

        return false;
    }

    private sealed class WmiBrightnessRestorePoint(string instanceName, byte brightness) : IBrightnessRestorePoint
    {
        public void Restore()
        {
            SetBrightness(instanceName, brightness);
        }
    }
}
