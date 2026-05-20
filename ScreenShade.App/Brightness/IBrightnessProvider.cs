namespace ScreenShade.App.Brightness;

internal interface IBrightnessProvider
{
    IReadOnlyList<IBrightnessRestorePoint> DimToMinimum(IReadOnlySet<string>? displayDeviceNames);
}
