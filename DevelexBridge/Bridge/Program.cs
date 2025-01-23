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
        
        ApplicationConfiguration.Initialize();
        Application.Run(new BridgeWindow());
    }
}