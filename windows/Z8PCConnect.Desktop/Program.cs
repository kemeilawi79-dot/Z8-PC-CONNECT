namespace Z8PCConnect.Desktop;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstance = new System.Threading.Mutex(true, @"Local\Z8PCConnect.Desktop", out var firstInstance);
        if (!firstInstance) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
