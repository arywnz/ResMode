using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

public class GameCandidate {
    public string Name, Folder, Hint;
    public List<string> Exes = new List<string>();
}

// Finds games outside any launcher by their fingerprints (engine and game-SDK files), and turns
// a pasted path (folder, .exe or shortcut) into a game ready to add.
public static class GameFinder {
    // Files that only games ship with. Common app runtimes (d3dcompiler, Electron, VC++) are deliberately absent.
    static readonly Dictionary<string, string> Markers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        { "UnityPlayer.dll", "Unity" }, { "GameAssembly.dll", "Unity" },
        { "steam_api64.dll", "Steam" }, { "steam_api.dll", "Steam" },
        { "EOSSDK-Win64-Shipping.dll", "Epic" }, { "Galaxy64.dll", "GOG" }, { "Galaxy.dll", "GOG" },
        { "uplay_r1_loader64.dll", "Ubisoft" }, { "upc_r2_loader64.dll", "Ubisoft" },
        { "nvngx_dlss.dll", "DLSS" }, { "libxess.dll", "XeSS" }, { "amd_fidelityfx_dx12.dll", "FSR" }, { "amd_ags_x64.dll", "AMD AGS" },
        { "bink2w64.dll", "Bink" }, { "binkw64.dll", "Bink" }, { "bink2w32.dll", "Bink" }, { "binkw32.dll", "Bink" },
        { "fmod.dll", "FMOD" }, { "fmodstudio.dll", "FMOD" }, { "fmodex64.dll", "FMOD" },
        { "PhysX3_x64.dll", "PhysX" }, { "PhysX_64.dll", "PhysX" }, { "discord_game_sdk.dll", "Discord SDK" },
        { "data.win", "GameMaker" }
    };

    // Folder names that wrap the real game root (Binaries\Win64, bin\x64...).
    static readonly HashSet<string> BinFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "bin", "bin32", "bin64", "binaries", "win32", "win64", "x86", "x64", "retail", "shipping", "release"
    };

    // Never walked: system areas, caches and big trees that never hold games.
    static readonly HashSet<string> SkipFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "Windows", "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs", "ProgramData", "$WinREAgent", "Config.Msi",
        "WindowsApps", "WinSxS", "Temp", "Packages", "node_modules", ".git", ".vs", "Microsoft", "Windows Kits", "Common Files",
        "dotnet", "NVIDIA Corporation", "Intel", "AMD", "Google", "Mozilla Firefox", "Microsoft Office", "Reference Assemblies",
        "WindowsPowerShell", "Internet Explorer", "Windows Defender", "Windows NT", "Cache", "Caches", "Code Cache", "GPUCache"
    };

    // Publishers whose executables are never games, even if a game file sits next to them.
    static readonly string[] NonGamePublishers = { "Microsoft", "Google", "Mozilla", "NVIDIA", "Advanced Micro Devices", "Intel", "Adobe", "Oracle", "Discord", "Spotify", "Valve" };

    // Store clients and their helpers ship game SDKs too; they are never games themselves.
    static readonly System.Text.RegularExpressions.Regex LauncherName = new System.Text.RegularExpressions.Regex(
        @"(?i)^(steam|steamwebhelper|epicgameslauncher|upc|ubisoftconnect|uplay.*|eadesktop|ealauncher|origin|galaxyclient.*|battle\.net.*|riotclient.*|socialclub.*|playgtav|rockstarservice|launcher|.*launcher|.*updater.*|.*service|qtwebengineprocess|.*webhelper)$");
    static readonly System.Text.RegularExpressions.Regex LauncherFolder = new System.Text.RegularExpressions.Regex(
        @"(?i)(launcher|social club|ubisoft connect|galaxy client|gog galaxy|battle\.net|ea desktop|riot client|^steam$|^origin$)");

    public static bool IsLauncherExe(string exe) { return LauncherName.IsMatch(Path.GetFileNameWithoutExtension(exe)) || Catalog.Helper(exe); }

    const int MaxDepth = 7;

    // Walks every fixed drive. onFound gets each new game as soon as it is found; onProgress the folder being read.
    public static void Search(Func<string, bool> known, Action<GameCandidate> onFound, Action<string> onProgress, CancellationToken cancel) {
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Tuple<string, int>>();
        foreach (var d in DriveInfo.GetDrives()) {
            try { if (d.DriveType == DriveType.Fixed && d.IsReady) queue.Enqueue(Tuple.Create(d.RootDirectory.FullName, 0)); } catch { }
        }
        int seen = 0;
        while (queue.Count > 0) {
            cancel.ThrowIfCancellationRequested();
            var item = queue.Dequeue();
            string dir = item.Item1;
            if (roots.Any(r => dir.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase))) continue;
            if (++seen % 50 == 0) onProgress(dir);
            try {
                var di = new DirectoryInfo(dir);
                if (item.Item2 > 0 && (di.Attributes & (FileAttributes.System | FileAttributes.ReparsePoint)) != 0) continue;
                var game = Detect(dir, known);
                if (game != null && roots.Add(game.Folder)) {
                    onFound(game);
                    continue;
                }
                if (item.Item2 >= MaxDepth) continue;
                foreach (var sub in di.EnumerateDirectories()) {
                    if (SkipFolders.Contains(sub.Name) || sub.Name.StartsWith(".")) continue;
                    queue.Enqueue(Tuple.Create(sub.FullName, item.Item2 + 1));
                }
            } catch (UnauthorizedAccessException) { } catch (IOException) { } catch (System.Security.SecurityException) { }
        }
    }

    // Looks for game fingerprints in one folder; returns the game it belongs to, or null.
    static GameCandidate Detect(string dir, Func<string, bool> known) {
        string marker = null;
        bool shipping = false;
        foreach (string f in Directory.EnumerateFiles(dir)) {
            string name = Path.GetFileName(f);
            string engine;
            if (Markers.TryGetValue(name, out engine)) { marker = engine; }
            else if (name.EndsWith("-Win64-Shipping.exe", StringComparison.OrdinalIgnoreCase)) { marker = "Unreal"; shipping = true; }
            else if (name.EndsWith(".pck", StringComparison.OrdinalIgnoreCase) && marker == null) marker = "Godot";
            if (shipping) break;
        }
        if (marker == null) return null;

        // Climb out of Binaries\Win64-style folders to the folder that holds the whole game.
        string root = dir;
        if (shipping) {
            for (int i = 0; i < 3 && Path.GetDirectoryName(root) != null; i++) root = Path.GetDirectoryName(root); // <Project>\Binaries\Win64 -> install root
        } else {
            while (BinFolders.Contains(Path.GetFileName(root)) && Path.GetDirectoryName(root) != null) root = Path.GetDirectoryName(root);
        }
        if (!Catalog.IsSafeGameRoot(root)) root = dir;
        if (!Catalog.IsSafeGameRoot(root) || root.Split('\\').Any(part => LauncherFolder.IsMatch(part))) return null;
        var game = Build(root, known, 5, marker);
        return game != null && game.Exes.Count > 0 ? game : null;
    }

    // Collects a game's executables (helpers, tiny stubs, other publishers' tools and known exes left out).
    static GameCandidate Build(string root, Func<string, bool> known, int depth, string hint) {
        var exes = new List<FileInfo>();
        var queue = new Queue<Tuple<string, int>>();
        queue.Enqueue(Tuple.Create(root, 0));
        int folders = 0;
        while (queue.Count > 0 && folders++ < 400) {
            var item = queue.Dequeue();
            try {
                foreach (var f in new DirectoryInfo(item.Item1).EnumerateFiles("*.exe")) {
                    // A folder holding a library game belongs to its launcher; leftover tools there are not a new game.
                    if (known(f.FullName)) return null;
                    if (f.Length < 100 * 1024 || IsLauncherExe(f.FullName) || IsOtherPublisher(f.FullName)) continue;
                    exes.Add(f);
                }
                if (item.Item2 < depth)
                    foreach (var sub in new DirectoryInfo(item.Item1).EnumerateDirectories())
                        if (!Catalog.IsHelperFolder(sub.Name) && !SkipFolders.Contains(sub.Name)) queue.Enqueue(Tuple.Create(sub.FullName, item.Item2 + 1));
            } catch (UnauthorizedAccessException) { } catch (IOException) { }
        }
        if (exes.Count == 0 || exes.Count > Catalog.MaxExesPerGame) return null;
        // Keep the likely game binaries: shipping exes first, then the biggest ones.
        var chosen = exes.OrderByDescending(f => f.Name.EndsWith("-Shipping.exe", StringComparison.OrdinalIgnoreCase)).ThenByDescending(f => f.Length).Take(6).ToList();
        return new GameCandidate { Folder = root, Hint = hint, Name = NameFor(root, chosen[0].FullName), Exes = chosen.Select(f => f.FullName).ToList() };
    }

    static bool IsOtherPublisher(string exe) {
        try {
            string company = FileVersionInfo.GetVersionInfo(exe).CompanyName ?? "";
            return NonGamePublishers.Any(p => company.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        } catch { return false; }
    }

    static readonly string[] GenericNames = { "", "BootstrapPackagedGame", "UnrealGame", "UE4Game", "UE5Game", "Unity", "Game", "Launcher" };

    static string NameFor(string root, string exe) {
        string product = "";
        try { product = (FileVersionInfo.GetVersionInfo(exe).ProductName ?? "").Trim(); } catch { }
        if (!GenericNames.Contains(product, StringComparer.OrdinalIgnoreCase) && product.Length < 80) return product;
        return Path.GetFileName(root).Replace('_', ' ').Trim();
    }

    // Pasted text -> games. Accepts a folder, an .exe or a .lnk shortcut, with or without quotes.
    public static List<GameCandidate> FromPath(string input, Func<string, bool> known) {
        string path = Environment.ExpandEnvironmentVariables((input ?? "").Trim().Trim('"', '\'').Trim());
        if (path == "") throw new Exception("Cole o caminho da pasta ou do .exe do jogo.");
        if (path.StartsWith("steam://", StringComparison.OrdinalIgnoreCase) || path.Contains("://"))
            throw new Exception("Isso é um link. Cole o caminho da pasta do jogo.");
        try { path = Path.GetFullPath(path); } catch { throw new Exception("Esse caminho não é válido."); }

        if (String.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) path = ShortcutTarget(path);
        if (File.Exists(path)) {
            if (!String.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) throw new Exception("Esse arquivo não é um .exe.");
            if (IsLauncherExe(path)) throw new Exception(Path.GetFileName(path) + " é um launcher, não o jogo.");
            if (known(path)) throw new Exception("Esse jogo já está na sua biblioteca.");
            string folder = Path.GetDirectoryName(path);
            return new List<GameCandidate> { new GameCandidate { Folder = folder, Name = NameFor(folder, path), Exes = new List<string> { path } } };
        }
        if (!Directory.Exists(path)) throw new Exception("Esse caminho não existe.");
        if (!Catalog.IsSafeGameRoot(path)) throw new Exception("Escolha a pasta do jogo, não a do disco ou do sistema.");

        // Fingerprinted games inside the folder first; otherwise every plausible executable in it.
        var found = new List<GameCandidate>();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Tuple<string, int>>();
        queue.Enqueue(Tuple.Create(path, 0));
        int folders = 0;
        while (queue.Count > 0 && folders++ < 3000) {
            var item = queue.Dequeue();
            if (roots.Any(r => item.Item1.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase))) continue;
            try {
                var game = Detect(item.Item1, known);
                if (game != null && roots.Add(game.Folder)) { found.Add(game); continue; }
                if (item.Item2 < 5)
                    foreach (var sub in Directory.EnumerateDirectories(item.Item1))
                        if (!SkipFolders.Contains(Path.GetFileName(sub))) queue.Enqueue(Tuple.Create(sub, item.Item2 + 1));
            } catch (UnauthorizedAccessException) { } catch (IOException) { }
        }
        if (found.Count == 0) {
            var loose = Build(path, known, 3, null);
            if (loose != null) found.Add(loose);
        }
        if (found.Count == 0) {
            if (Build(path, x => false, 3, null) != null) throw new Exception("Os jogos dessa pasta já estão na sua biblioteca.");
            throw new Exception("Não achei nenhum jogo nessa pasta.");
        }
        return found;
    }

    static string ShortcutTarget(string lnk) {
        object com = null, link = null;
        try {
            com = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic shell = com;
            link = shell.CreateShortcut(lnk);
            dynamic l = link;
            string target = (string)l.TargetPath;
            if (String.IsNullOrEmpty(target)) throw new Exception("Esse atalho abre pelo launcher. Cole a pasta do jogo.");
            return target;
        } finally {
            if (link != null) Marshal.FinalReleaseComObject(link);
            if (com != null) Marshal.FinalReleaseComObject(com);
        }
    }
}
