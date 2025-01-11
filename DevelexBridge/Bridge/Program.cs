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
        // TODO: for future eyetrackers do some kind of merging dll
        var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EyeTrackers", "EyeLogic", "Libs");
        SetDllDirectory(dllPath);
        
        ApplicationConfiguration.Initialize();
        Application.Run(new BridgeWindow());
    }
}