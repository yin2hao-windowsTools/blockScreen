namespace ScreenShade.App;

static class Program
{
    private const string MutexName = "A1ScreenShade.SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show($"{AppInfo.Name} 已在运行。", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ScreenShadeApplicationContext());
    }    
}
