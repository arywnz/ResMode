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
    public const string Release = "0.2.2-alpha.23";
}

public class GameRule { public string Name; public string Exe; public string Source; }
public class GameMode { public string Key; public int Width, Height; }

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
    // While a game runs, the monitor beside the game's is centred on it instead of hanging from its top edge.
    public bool CenterSecondMonitor = true;
    // The first-run walkthrough has been shown (or skipped).
    public bool TourDone = false;
    // History: "all", "games" (game sessions only) or "off"; errors are always kept.
    public string LogLevel = "all";
    // Looks for a newer GitHub release at startup and every 15 minutes; AnnouncedUpdate keeps the tray balloon to once per version.
    public bool CheckUpdates = true;
    public string AnnouncedUpdate = "";
    public List<string> IncludedGames = new List<string>();
    public List<string> ExcludedGames = new List<string>();
    public List<GameRule> Games = new List<GameRule>();
    // Per-game resolution: Width 0 keeps that game at the normal resolution; no entry means the general one.
    public List<GameMode> GameModes = new List<GameMode>();
    // Order of the library: "name", "played" (most time first) or "recent".
    public string SortMode = "name";
}

// Where another monitor sits on the desktop (in each monitor's own pixels), kept so it can go back after the game.
public class MonitorSpot { public string Device; public int X, Y, Width, Height; }

public class Recovery { public string Device; public int Width, Height, Hz, Bits, OwnerPid; public List<DesktopLayout.Saved> Windows=new List<DesktopLayout.Saved>(); public List<MonitorSpot> Others=new List<MonitorSpot>(); }

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

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

    // Process that owns the window in front right now; 0 when there is none.
    public static uint ForegroundPid() {
        uint pid;
        IntPtr window = GetForegroundWindow();
        return window != IntPtr.Zero && GetWindowThreadProcessId(window, out pid) != 0 ? pid : 0;
    }

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DisplayDevice {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string description;
        public int flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string key;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string device, int index, ref DisplayDevice value, int flags);

    // Every monitor that is part of the desktop, with its true position and size (not DPI-scaled).
    public static List<MonitorSpot> ActiveMonitors() {
        var list = new List<MonitorSpot>();
        for (int i = 0; ; i++) {
            var d = new DisplayDevice { cb = Marshal.SizeOf(typeof(DisplayDevice)) };
            if (!EnumDisplayDevices(null, i, ref d, 0)) break;
            if ((d.flags & 1) == 0) continue; // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
            Mode m = Empty();
            if (EnumDisplaySettingsEx(d.name, -1, ref m, 0)) list.Add(new MonitorSpot { Device = d.name, X = m.x, Y = m.y, Width = m.width, Height = m.height });
        }
        return list;
    }

    // Moves a monitor without changing its mode. The position is written to the registry like the game resolution.
    public static int SetPosition(string device, int x, int y) {
        Mode m = Current(device);
        m.x = x;
        m.y = y;
        m.fields = 0x20 | 0x40000 | 0x80000 | 0x100000 | 0x400000; // position, bits, width, height, frequency
        m.extra = 0;
        return ChangeDisplaySettingsEx(device, ref m, IntPtr.Zero, 1, IntPtr.Zero);
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

    // First-run pick: the largest mode the driver offers above the desktop's. The person can lower it in Settings
    // or per game; the default is the most the screen can be made to render.
    public static bool PickGameMode(string device, int desktopW, int desktopH, out int w, out int h) {
        var modes = LargerModes(device, desktopW, desktopH);
        w = h = 0;
        if (modes.Count == 0) return false;
        var best = modes.OrderByDescending(m => m.width * (double)m.height).First();
        w = best.width;
        h = best.height;
        return true;
    }

    public static int Set(string device, Mode mode, bool test) {
        mode.fields = 0x40000 | 0x80000 | 0x100000 | 0x400000; // bits, width, height, frequency
        mode.extra = 0;
        // CDS_UPDATEREGISTRY, not the temporary CDS_FULLSCREEN: a temporary mode is undone as soon as any program (a game
        // losing focus on Alt+Tab, say) calls ChangeDisplaySettings(NULL), which sends the desktop back to 1080p mid-game.
        // The desktop mode is written back the same way when the game closes (Store.Restore), and again at the next start
        // if the PC went down in between (restauracao.xml).
        return ChangeDisplaySettingsEx(device, ref mode, IntPtr.Zero, test ? 2 : 1, IntPtr.Zero);
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
