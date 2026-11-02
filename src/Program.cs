using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Native DLLs (user32, dwmapi...) load only from System32, never from the app folder or the working directory.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
public static class Program {
    // Each command lives in its own method so the lightweight /guard process does not load WinForms.
    [STAThread]
    public static void Main(string[] args) {
        Native.SetProcessDPIAware();
        string command = args.Length > 0 ? args[0] : "";
        string arg = args.Length > 1 ? args[1] : null;
        if (command == "/where") { using (var self = Process.GetCurrentProcess()) File.WriteAllText(arg, Path.GetDirectoryName(Native.Exe(self))); return; }
        switch (command) {
            case "/selftest": SelfTest(arg); return;
            case "/preferencestest": GamePreferences.Tests(arg); return;
        }
        Store.UseUserData();
        switch (command) {
            case "/guard": Guard(Int32.Parse(arg)); return;
            case "/shellguard": ShellRefresh.Guard(Int32.Parse(arg),args[2]); return;
            case "/shellrefresh": ShellRefresh.TryRun(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(arg))); return;
            case "/restore": Store.Restore(); return;
        }
        Run(args.Contains("/background"));
    }

    static void Run(bool background) {
        bool created;
        using (var mutex = new Mutex(true, AppInfo.MutexName, out created)) {
            if (!created) {
                if (!background) {
                    try {
                        using (var signal = EventWaitHandle.OpenExisting(AppInfo.ShowEventName)) signal.Set();
                    } catch { }
                }
                return;
            }
            try {
                using (var self = Process.GetCurrentProcess())
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "/guard " + self.Id) { UseShellExecute = false, CreateNoWindow = true });
                // A bug in a click handler is logged and shown, not fatal; a crash still restores the desktop first.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => {
                    Store.Log("Erro inesperado: " + e.Exception, LogKind.Error);
                    MessageBox.Show(e.Exception.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                    Store.Log("Falha grave: " + e.ExceptionObject, LogKind.Error);
                    Store.Restore();
                };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContext(!background));
            } catch (Exception e) {
                // Quit or update chosen from the tray menu: the menu was disposed while Windows was still closing it. Nothing to report.
                var disposed = e as ObjectDisposedException;
                if (disposed == null || disposed.ObjectName != "ToolStripDropDown") {
                    Store.Log(e.ToString(), LogKind.Error);
                    MessageBox.Show(e.Message, AppInfo.Name);
                }
            } finally {
                Store.Restore();
            }
        }
    }

    // Separate process that restores the desktop if the main process dies unexpectedly.
    static void Guard(int pid) {
        Native.TrimMemory();
        try {
            using (var p = Process.GetProcessById(pid)) p.WaitForExit();
        } catch { }
        if (File.Exists(Store.State)) Store.Restore(pid);
    }

    static void SelfTest(string report) {
        var results = new List<string>();
        DesktopLayout.GeometryTests();
        results.Add("PASS: coordenadas relativas nos monitores à direita/negativas, ida e volta e WINDOWPLACEMENT.");
        ShellRefresh.ReadinessTests();
        results.Add("PASS: Explorer só pronto com área de trabalho e barra responsivas no mesmo processo e estáveis; PID isolado não confirma recuperação.");

        var helpers = new[] {
            @"C:\Games\Game\createdump.exe", @"C:\Games\Game\crs-handler.exe", @"C:\Games\Game\launcher.exe",
            @"C:\Games\Game\EasyAntiCheat\eac.exe", @"C:\Games\Game\_CommonRedist\setup.exe", @"C:\Games\Game\unins000.exe",
            @"C:\Games\Game\UnityCrashHandler64.exe", @"C:\Games\Game\CrashReportClient.exe"
        };
        foreach (string helper in helpers)
            if (!Catalog.Helper(helper)) throw new Exception("Helper não filtrado: " + helper);
        // Sample paths only (nothing is read from disk): the filter must keep real game binaries.
        var games = new[] {
            @"X:\Library\steamapps\common\Some Game\End\Binaries\Win64\somegame_.exe",
            @"X:\Library\steamapps\common\Another Game\another-i.exe",
            @"X:\Library\steamapps\common\Crash Bandicoot N. Sane Trilogy\CrashBandicootNSaneTrilogy.exe",
            @"X:\Library\steamapps\common\Crash Team Racing\CTR.exe",
            @"X:\Games\Update Edition\Game.exe",
            @"X:\Games\Support Squad\SupportSquad.exe"
        };
        foreach (string game in games)
            if (Catalog.Helper(game)) throw new Exception("Jogo filtrado: " + game);
        results.Add("PASS: auxiliares filtrados; jogos com 'Crash', 'Update' ou 'Support' no nome preservados.");

        foreach (string unsafeRoot in new[] { Path.GetPathRoot(Environment.SystemDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.SystemDirectory })
            if (Catalog.IsSafeGameRoot(unsafeRoot)) throw new Exception("Pasta do sistema aceita como jogo: " + unsafeRoot);
        if (!Catalog.IsSafeGameRoot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Some Game")))
            throw new Exception("Pasta de jogo recusada.");
        results.Add("PASS: raízes de disco e pastas do sistema nunca viram jogo.");

        using (var p = Process.GetCurrentProcess()) {
            string exe = Native.Exe(p);
            if (!Path.IsPathRooted(exe) || !String.Equals(Path.GetFileName(exe), Path.GetFileName(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Caminho de processo incorreto.");
            if (!Native.ProcessIds().Contains(p.Id))
                throw new Exception("Lista de processos não encontrou o próprio processo.");
        }
        results.Add("PASS: lista leve de processos e caminho completo do executável.");

        if (Marshal.SizeOf(typeof(Native.Mode)) != 220) throw new Exception("DEVMODE inválido.");
        results.Add("PASS: estrutura Win32 Unicode de 220 bytes.");
        File.WriteAllLines(report, results);
    }
}
