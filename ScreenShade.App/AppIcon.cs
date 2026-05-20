namespace ScreenShade.App;

internal static class AppIcon
{
    public static Icon Load()
    {
        var iconStream = typeof(AppIcon).Assembly.GetManifestResourceStream("AppIcon.ico");
        if (iconStream is null)
        {
            return (Icon)SystemIcons.Application.Clone();
        }

        using (iconStream)
        {
            return new Icon(iconStream);
        }
    }
}
