using System.Runtime.InteropServices;

namespace ScreenShade.App.Brightness;

internal sealed class PhysicalMonitorBrightnessProvider : IBrightnessProvider
{
    public IReadOnlyList<IBrightnessRestorePoint> DimToMinimum(IReadOnlySet<string>? displayDeviceNames)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var restorePoints = new List<IBrightnessRestorePoint>();

        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitorHandle, _, _, _) =>
            {
                if (displayDeviceNames is { Count: > 0 } &&
                    !displayDeviceNames.Contains(GetDisplayDeviceName(monitorHandle)))
                {
                    return true;
                }

                restorePoints.AddRange(DimMonitor(monitorHandle));
                return true;
            },
            IntPtr.Zero);

        return restorePoints;
    }

    private static string GetDisplayDeviceName(IntPtr monitorHandle)
    {
        var monitorInfo = new NativeMethods.MonitorInfoEx
        {
            Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>()
        };

        return NativeMethods.GetMonitorInfo(monitorHandle, ref monitorInfo)
            ? monitorInfo.DeviceName
            : string.Empty;
    }

    private static IReadOnlyList<IBrightnessRestorePoint> DimMonitor(IntPtr monitorHandle)
    {
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(monitorHandle, out var monitorCount) || monitorCount == 0)
        {
            return [];
        }

        var physicalMonitors = new NativeMethods.PhysicalMonitor[monitorCount];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(monitorHandle, monitorCount, physicalMonitors))
        {
            return [];
        }

        var restorePoints = new List<IBrightnessRestorePoint>();

        foreach (var physicalMonitor in physicalMonitors)
        {
            if (!NativeMethods.GetMonitorBrightness(
                    physicalMonitor.Handle,
                    out var minimumBrightness,
                    out var currentBrightness,
                    out _))
            {
                NativeMethods.DestroyPhysicalMonitor(physicalMonitor.Handle);
                continue;
            }

            if (NativeMethods.SetMonitorBrightness(physicalMonitor.Handle, minimumBrightness))
            {
                restorePoints.Add(new PhysicalMonitorBrightnessRestorePoint(physicalMonitor.Handle, currentBrightness));
            }
            else
            {
                NativeMethods.DestroyPhysicalMonitor(physicalMonitor.Handle);
            }
        }

        return restorePoints;
    }

    private sealed class PhysicalMonitorBrightnessRestorePoint(IntPtr monitorHandle, uint brightness) : IBrightnessRestorePoint
    {
        public void Restore()
        {
            try
            {
                NativeMethods.SetMonitorBrightness(monitorHandle, brightness);
            }
            finally
            {
                NativeMethods.DestroyPhysicalMonitor(monitorHandle);
            }
        }
    }

    private static partial class NativeMethods
    {
        internal delegate bool MonitorEnumProc(IntPtr monitorHandle, IntPtr hdc, IntPtr clipRect, IntPtr data);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct PhysicalMonitor
        {
            public IntPtr Handle;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct MonitorInfoEx
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr clipRect,
            MonitorEnumProc callback,
            IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfoEx monitorInfo);

        [DllImport("dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr monitorHandle,
            out uint numberOfPhysicalMonitors);

        [DllImport("dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr monitorHandle,
            uint physicalMonitorArraySize,
            [Out] PhysicalMonitor[] physicalMonitorArray);

        [DllImport("dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorBrightness(
            IntPtr monitorHandle,
            out uint minimumBrightness,
            out uint currentBrightness,
            out uint maximumBrightness);

        [DllImport("dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetMonitorBrightness(IntPtr monitorHandle, uint newBrightness);

        [DllImport("dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyPhysicalMonitor(IntPtr monitorHandle);
    }
}
