using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public static class AppInfo {
    public const string Name = "ResMode";
    public const string ExeName = "ResMode.exe";
    public const string IconName = "ResMode.ico";
    public const string LogName = "ResMode.log";
    public const string MutexName = "Local\\ResMode.ResolutionWatcher";
    public const string ShowEventName = "Local\\ResMode.Show";
    // Shown to the user and compared with GitHub releases; keep in step with AssemblyInfo.cs and the MSI.
    public const string Release = "0.2.2-alpha.3";
}

public class GameRule { public string Name; public string Exe; public string Source; }

public class Settings {
    // 0 = not chosen yet; picked on first run from what this PC's driver offers.
    public int Width, Height;
    // Last desktop mode seen before switching to the game resolution; 0 = read from the monitor on first run.
    public int DesktopWidth, DesktopHeight;
    public string Display = "";
    public bool AutoRefreshTaskbar = false;
    public bool StartPaused = false;
    // Competitive games stay at the desktop resolution unless the user turned them on (IncludedGames).
    public bool SkipCompetitive = true;
    public bool CompetitiveNotified = false;
    // The first-run search for games outside the launchers has finished.
    public bool FirstSearchDone = false;
    // History: "all", "games" (game sessions only) or "off"; errors are always kept.
    public string LogLevel = "all";
    // Looks for a newer GitHub release at startup and twice a day; AnnouncedUpdate keeps the tray balloon to once per version.
    public bool CheckUpdates = true;
    public string AnnouncedUpdate = "";
    public List<string> IncludedGames = new List<string>();
    public List<string> ExcludedGames = new List<string>();
    public List<GameRule> Games = new List<GameRule>();
}

public class Recovery { public string Device; public int Width, Height, Hz, Bits, OwnerPid; public List<DesktopLayout.Saved> Windows=new List<DesktopLayout.Saved>(); }

public static class Native {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Mode {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string device;
        public short spec, driver, size, extra;
        public int fields, x, y, orientation, fixedOutput;
        public short color, duplex, yres, tt, collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string form;
        public short logpixels;
        public int bits, width, height, flags, hz, icmMethod, icmIntent, media, dither, reserved1, reserved2, panWidth, panHeight;
    }

    [StructLayout(LayoutKind.Sequential)] struct LastInput { public uint size, time; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettingsEx(string device, int mode, ref Mode value, int flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ChangeDisplaySettingsEx(string device, ref Mode value, IntPtr hwnd, int flags, IntPtr param);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput value);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr handle, int flags, StringBuilder path, ref int size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("psapi.dll")] static extern bool EnumProcesses([Out] uint[] ids, int size, out int needed);
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

    public static long IdleMs() {
        var v = new LastInput { size = 8 };
        return GetLastInputInfo(ref v) ? unchecked((uint)Environment.TickCount - v.time) : 0;
    }

    // Display modes ------------------------------------------------------

    public static Mode Empty() {
        var m = new Mode();
        m.size = (short)Marshal.SizeOf(typeof(Mode));
        return m;
    }

    public static Mode Current(string device) {
        Mode m = Empty();
        if (!EnumDisplaySettingsEx(device, -1, ref m, 0)) throw new Exception("Não foi possível consultar o monitor.");
        return m;
    }

    public static List<Mode> Modes(string device) {
        var list = new List<Mode>();
        for (int i = 0; ; i++) {
            Mode m = Empty();
            if (!EnumDisplaySettingsEx(device, i, ref m, 0)) break;
            list.Add(m);
        }
        return list;
    }

    public static Mode Find(string device, int w, int h, int hz) {
        var match = Modes(device).Where(m => m.width == w && m.height == h && m.bits == 32).OrderBy(m => Math.Abs(m.hz - hz)).ToList();
        if (match.Count == 0) throw new Exception(w + " × " + h + " não está disponível neste monitor. " + EnableHint);
        return match[0];
    }

    public const string EnableHint = "Ative o DSR/DLDSR (NVIDIA) ou o VSR (AMD), ou crie uma resolução personalizada no painel da placa de vídeo.";

    // Distinct 32-bit resolutions larger than the given desktop, with the highest refresh rate each supports.
    public static List<Mode> LargerModes(string device, int desktopW, int desktopH) {
        double desktop = desktopW * (double)desktopH;
        return Modes(device).Where(m => m.bits == 32 && m.width * (double)m.height > desktop)
            .GroupBy(m => m.width * 100000 + m.height).Select(g => g.OrderByDescending(m => m.hz).First())
            .OrderBy(m => m.width * (double)m.height).ToList();
    }

    // First-run pick: the mode closest to 2.25× the desktop's pixels (DLDSR's sharpest factor), if the driver offers any.
    public static bool PickGameMode(string device, int desktopW, int desktopH, out int w, out int h) {
        var modes = LargerModes(device, desktopW, desktopH);
        w = h = 0;
        if (modes.Count == 0) return false;
        double desktop = desktopW * (double)desktopH;
        var best = modes.OrderBy(m => Math.Abs(m.width * (double)m.height / desktop - 2.25)).First();
        w = best.width;
        h = best.height;
        return true;
    }

    public static int Set(string device, Mode mode, bool test) {
        mode.fields = 0x40000 | 0x80000 | 0x100000 | 0x400000; // bits, width, height, frequency; no registry persistence
        mode.extra = 0;
        return ChangeDisplaySettingsEx(device, ref mode, IntPtr.Zero, test ? 2 : 4, IntPtr.Zero);
    }

    // Processes -----------------------------------------------------------

    static uint[] pidBuffer = new uint[2048];

    // PIDs only: ~0.04 ms per call, versus ~5 ms for a Toolhelp snapshot or Process.GetProcesses().
    public static IEnumerable<int> ProcessIds() {
        int needed;
        while (true) {
            if (!EnumProcesses(pidBuffer, pidBuffer.Length * 4, out needed)) return new int[0];
            if (needed < pidBuffer.Length * 4) break;
            pidBuffer = new uint[pidBuffer.Length * 2];
        }
        var result = new int[needed / 4];
        for (int i = 0; i < result.Length; i++) result[i] = (int)pidBuffer[i];
        return result;
    }

    public static string ExePath(int pid) {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return "";
        try {
            var b = new StringBuilder(1024);
            int len = b.Capacity;
            return QueryFullProcessImageName(h, 0, b, ref len) ? b.ToString() : "";
        } finally { CloseHandle(h); }
    }

    public static string Exe(Process p) { return ExePath(p.Id); }

    public static void TrimMemory() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        using (var p = Process.GetCurrentProcess()) SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
    }
}
