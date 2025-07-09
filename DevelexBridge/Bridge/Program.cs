using System.Runtime.InteropServices;

namespace Bridge;

static class Program
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetDllDirectory(string lpPathName);
    
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EyeTrackers", "Libs");
        SetDllDirectory(dllPath);

        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var develexFolder = Path.Combine(appDataFolder, "Develex");

        if (!Directory.Exists(develexFolder))
        {
            Directory.CreateDirectory(develexFolder);
        }

        var develexCrashLogPath = Path.Combine(develexFolder, "crashlog.txt");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            File.AppendAllText(develexCrashLogPath, $"{DateTime.Now} --> " + args.ExceptionObject + Environment.NewLine);
        };
        
        ApplicationConfiguration.Initialize();
        Application.Run(new BridgeWindow());
    }
}