using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

public class CatalogEntry {
    public string Id, Stamp, Type;
    public List<GameRule> Games = new List<GameRule>();
}

public class CatalogCache { public List<CatalogEntry> Entries = new List<CatalogEntry>(); }

public static class Catalog {
    // Bump when the executable filter changes so cached entries are rescanned.
    const string StampVersion = "v3";

    // Folders that only hold redistributables, anti-cheat or tooling. Matched against whole folder names only.
    static readonly HashSet<string> HelperFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "_CommonRedist", "CommonRedist", "redist", "redistributable", "redistributables", "support", "installer", "installers",
        "__Installer", "tools", "modding", "crashreporter", "crashpad", "EasyAntiCheat", "EasyAntiCheat_EOS", "BattlEye",
        "DirectX", "vcredist", "prereqs", "prerequisites"
    };

    // Executable names (without .exe) of helpers. Matched against the file name only, never the whole path,
    // so games like "Crash Bandicoot" or folders named "Update" are not discarded.
    static readonly Regex HelperExe = new Regex(
        @"^(unins\d*|uninstall.*|setup.*|.*installer|install|update|updater|.*_updater|crashreport.*|.*crashhandler.*|.*crash_?reporter.*" +
        @"|crashpad.*|createdump|crs-.*|.*bugreport.*|.*webhelper|benchmark.*|cefsubprocess|.*cef.*helper|eac_launcher" +
        @"|start_protected_game|.*launcher.*|configtool|.*config(uration)?tool|configuration|.*settingstool|steamworks.*" +
        @"|.*redist.*|dxsetup|.*prereq.*|unitycrashhandler.*|leagueclient.*|riotclient.*|bugsplat.*|.*vanguard.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsHelperFolder(string folderName) { return HelperFolders.Contains(folderName); }

    // A game folder must be a specific folder, never a drive root, the Windows folder or a shared system/user folder.
    // Guards against bogus or malicious registry entries that would turn every program into a "game".
    public static bool IsSafeGameRoot(string root) {
        try {
            root = Path.GetFullPath(root).TrimEnd('\\');
            if (root.Length <= 3 || String.Equals(Path.GetPathRoot(root).TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase)) return false;
            var shared = new[] {
                Environment.SpecialFolder.Windows, Environment.SpecialFolder.System, Environment.SpecialFolder.SystemX86,
                Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonProgramFiles,
                Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.Desktop, Environment.SpecialFolder.MyDocuments
            };
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
            if (root.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase)) return false;
            foreach (var f in shared) {
                string p = Environment.GetFolderPath(f).TrimEnd('\\');
                if (p.Length > 0 && String.Equals(p, root, StringComparison.OrdinalIgnoreCase)) return false;
            }
            string users = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return !String.Equals(users, root, StringComparison.OrdinalIgnoreCase);
        } catch { return false; }
    }

    // More executables than this in one install is not a single game (or the folder is wrong).
    public const int MaxExesPerGame = 40;

    public static bool Helper(string exePath) {
        string dir = Path.GetDirectoryName(exePath) ?? "";
        foreach (string part in dir.Split('\\', '/'))
            if (IsHelperFolder(part)) return true;
        return HelperExe.IsMatch(Path.GetFileNameWithoutExtension(exePath));
    }

    static string Value(string text, string key) {
        var m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s+\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value.Replace("\\\\", "\\") : "";
    }

    static IEnumerable<string> SteamLibraries() {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var k = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam")) {
            if (k != null) {
                string root = Convert.ToString(k.GetValue("SteamPath"));
                if (Directory.Exists(root)) roots.Add(root);
            }
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (string root in roots.ToArray()) {
            string f = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(f)) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"path\"\\s+\"([^\"]+)\""))
                roots.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
        }
        return roots.Where(Directory.Exists).Select(x => Path.Combine(x, "steamapps")).Where(Directory.Exists);
    }

    class ShortWebClient : WebClient {
        protected override WebRequest GetWebRequest(Uri u) {
            var r = base.GetWebRequest(u);
            r.Timeout = 5000;
            var h = r as HttpWebRequest;
            if (h != null) h.ReadWriteTimeout = 5000;
            return r;
        }
    }

    static string Type(string id) {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        using (var web = new ShortWebClient()) {
            string json = web.DownloadString("https://store.steampowered.com/api/appdetails?appids=" + id + "&filters=basic");
            var all = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            var app = (Dictionary<string, object>)all[id];
            if (!Convert.ToBoolean(app["success"])) return "unknown";
            return Convert.ToString(((Dictionary<string, object>)app["data"])["type"]);
        }
    }

    // Steam installs runtimes, SDKs and soundtracks next to games; without the store's answer the name has to tell them apart.
    static readonly Regex SteamTool = new Regex(@"(?i)redistributable|steamworks|linux runtime|^proton\b|steamvr|\bsdk\b|dedicated server|soundtrack|\bost\b|artbook|wallpaper engine|authoring tools|\bmod tools\b|^source filmmaker|spacewar");
    public static bool LooksLikeSteamTool(string name) { return SteamTool.IsMatch(name ?? ""); }

    static List<GameRule> FindExes(string root, string name, string source, Func<bool> stop) {
        var games = new List<GameRule>();
        var queue = new Queue<Tuple<string, int>>();
        queue.Enqueue(Tuple.Create(root, 0));
        int count = 0;
        while (queue.Count > 0 && count++ < 1500) {
            if (stop()) throw new OperationCanceledException();
            var d = queue.Dequeue();
            try {
                foreach (string f in Directory.EnumerateFiles(d.Item1, "*.exe", SearchOption.TopDirectoryOnly))
                    if (!Helper(f)) games.Add(new GameRule { Name = name, Exe = f, Source = source });
                if (d.Item2 < 7)
                    foreach (string sub in Directory.EnumerateDirectories(d.Item1))
                        if (!IsHelperFolder(Path.GetFileName(sub))) queue.Enqueue(Tuple.Create(sub, d.Item2 + 1));
            } catch (UnauthorizedAccessException) { } catch (IOException) { }
            Thread.Sleep(2);
        }
        return games;
    }

    public static List<GameRule> Scan(bool force) {
        Thread.CurrentThread.Priority = ThreadPriority.Lowest;
        string file = Path.Combine(Store.Root, "catalogo.xml");
        CatalogCache cache;
        try { cache = Store.Read<CatalogCache>(file); } catch { cache = new CatalogCache(); }
        var next = new CatalogCache();
        var result = new List<GameRule>();
        Func<bool> stop = delegate { return !force && Native.IdleMs() < 30000; };

        bool storeDown = false; // after one failed store lookup the rest of this scan skips the store
        foreach (string library in SteamLibraries())
            foreach (string manifest in Directory.EnumerateFiles(library, "appmanifest_*.acf")) {
                if (stop()) throw new OperationCanceledException();
                try {
                    string text = File.ReadAllText(manifest), id = Value(text, "appid"), name = Value(text, "name"), folder = Value(text, "installdir");
                    if (!Regex.IsMatch(id, @"^\d+$") || String.IsNullOrWhiteSpace(folder)) continue;
                    string common = Path.GetFullPath(Path.Combine(library, "common"));
                    string root = Path.GetFullPath(Path.Combine(common, folder));
                    if (!root.StartsWith(common + "\\", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(root)) continue;

                    var info = new FileInfo(manifest);
                    string stamp = StampVersion + ":" + info.LastWriteTimeUtc.Ticks + ":" + info.Length + ":" + DateTime.UtcNow.ToString("yyyyMMdd");
                    var previous = cache.Entries.FirstOrDefault(e => e.Id == id && e.Games.All(g => g.Exe.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)));
                    if (previous != null && previous.Stamp == stamp && previous.Type != "unknown") {
                        previous.Games = previous.Games.Where(g => File.Exists(g.Exe) && !Helper(g.Exe)).ToList();
                        next.Entries.Add(previous);
                        result.AddRange(previous.Games);
                        continue;
                    }
                    string type = previous != null && previous.Type != "unknown" ? previous.Type : "unknown";
                    if (type == "unknown" && !storeDown) {
                        // The store can be unreachable, rate-limited, or not know the app (delisted games); that must not hide the library.
                        try { type = Type(id); } catch (Exception e) { storeDown = true; Store.Log("Steam Store sem resposta (" + e.Message + "). O resto da biblioteca é identificado pelo nome.", LogKind.Library); }
                    }
                    var entry = new CatalogEntry { Id = id, Stamp = stamp, Type = type };
                    if (type == "game" || type == "demo" || (type == "unknown" && !LooksLikeSteamTool(name))) entry.Games = FindExes(root, name, "Steam:" + id, stop);
                    next.Entries.Add(entry);
                    result.AddRange(entry.Games);
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    Store.Log("Erro ao ler " + Path.GetFileName(manifest) + ": " + e.Message, LogKind.Error);
                    var old = cache.Entries.FirstOrDefault(x => Path.GetFileName(manifest) == "appmanifest_" + x.Id + ".acf");
                    if (old != null) {
                        next.Entries.Add(old);
                        result.AddRange(old.Games.Where(g => File.Exists(g.Exe)));
                    }
                }
            }

        // Epic provides the main launch executable and game category in its local manifest.
        string epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (Directory.Exists(epic))
            foreach (string f in Directory.EnumerateFiles(epic, "*.item")) {
                if (stop()) throw new OperationCanceledException();
                try {
                    var json = new JavaScriptSerializer();
                    var j = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(f));
                    if (!j.ContainsKey("AppCategories") || !j.ContainsKey("InstallLocation") || !j.ContainsKey("LaunchExecutable")) continue;
                    if (!Regex.IsMatch(json.Serialize(j["AppCategories"]), "\"games\"", RegexOptions.IgnoreCase)) continue;
                    string root = Path.GetFullPath(Convert.ToString(j["InstallLocation"]));
                    string exe = Path.GetFullPath(Path.Combine(root, Convert.ToString(j["LaunchExecutable"])));
                    if (exe.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) && IsSafeGameRoot(root) && File.Exists(exe))
                        result.Add(new GameRule { Name = Convert.ToString(j["DisplayName"]), Exe = exe, Source = "Epic" });
                } catch (Exception e) { Store.Log("Erro ao ler a biblioteca da Epic: " + e.Message, LogKind.Error); }
            }

        foreach (var g in OtherLaunchers(cache, next, stop)) result.Add(g);

        Store.Write(file, next);
        return result;
    }

    // Launchers that register installs in the registry: GOG (exact exe), and Ubisoft, EA, Battle.net,
    // Riot and Rockstar (install folder only, so the folder is scanned like a Steam game).
    static List<GameRule> OtherLaunchers(CatalogCache cache, CatalogCache next, Func<bool> stop) {
        var result = new List<GameRule>();
        var lm = Registry.LocalMachine;

        using (var games = lm.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games"))
            if (games != null)
                foreach (string id in games.GetSubKeyNames()) {
                    using (var k = games.OpenSubKey(id)) {
                        string exe = Convert.ToString(k.GetValue("exe")), name = Convert.ToString(k.GetValue("gameName"));
                        if (File.Exists(exe) && !Helper(exe) && IsSafeGameRoot(Path.GetDirectoryName(Path.GetFullPath(exe)))) result.Add(new GameRule { Name = name, Exe = Path.GetFullPath(exe), Source = "GOG" });
                    }
                }

        var folders = new List<Tuple<string, string, string>>(); // source, name, folder
        var uninstall = UninstallEntries();
        using (var installs = lm.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"))
            if (installs != null)
                foreach (string id in installs.GetSubKeyNames()) {
                    string dir = Launchers.RegString(installs, id, "InstallDir");
                    var entry = uninstall.FirstOrDefault(u => u.Key == "Uplay Install " + id);
                    if (dir != null) folders.Add(Tuple.Create("Ubisoft", entry != null ? entry.Name : Path.GetFileName(dir.TrimEnd('\\', '/')), dir));
                }
        foreach (var u in uninstall) {
            if (u.Folder == null) continue;
            if (u.Publisher.StartsWith("Electronic Arts") && !u.Name.StartsWith("EA app")) folders.Add(Tuple.Create("EA", u.Name, u.Folder));
            else if (u.Publisher.StartsWith("Blizzard") && u.Name != "Battle.net") folders.Add(Tuple.Create("Battle.net", u.Name, u.Folder));
            else if (u.Publisher.StartsWith("Riot Games") && !u.Name.StartsWith("Riot ")) folders.Add(Tuple.Create("Riot", u.Name, u.Folder));
        }
        using (var rockstar = lm.OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games"))
            if (rockstar != null)
                foreach (string name in rockstar.GetSubKeyNames()) {
                    if (name == "Launcher" || name.Contains("Social Club")) continue;
                    string dir = Launchers.RegString(rockstar, name, "InstallFolder"); // Steam copies use InstallFolderSteam and are already listed.
                    if (dir != null) folders.Add(Tuple.Create("Rockstar", name, dir));
                }

        foreach (var f in folders) {
            if (stop()) throw new OperationCanceledException();
            try {
                string root = Path.GetFullPath(f.Item3).TrimEnd('\\');
                if (!Directory.Exists(root) || !IsSafeGameRoot(root)) continue;
                string id = f.Item1.ToLowerInvariant() + ":" + root.ToLowerInvariant();
                string stamp = StampVersion + ":" + Directory.GetLastWriteTimeUtc(root).Ticks + ":" + DateTime.UtcNow.ToString("yyyyMMdd");
                var previous = cache.Entries.FirstOrDefault(e => e.Id == id && e.Stamp == stamp);
                var entry = previous ?? new CatalogEntry { Id = id, Stamp = stamp, Type = "game", Games = FindExes(root, f.Item2, f.Item1, stop) };
                entry.Games = entry.Games.Count > MaxExesPerGame ? new List<GameRule>() : entry.Games.Where(g => File.Exists(g.Exe)).ToList();
                next.Entries.Add(entry);
                result.AddRange(entry.Games);
            } catch (OperationCanceledException) { throw; } catch (Exception e) { Store.Log("Erro ao ler a biblioteca " + f.Item1 + ": " + e.Message, LogKind.Error); }
        }
        return result;
    }

    class UninstallEntry { public string Key, Name, Publisher, Folder; }

    static List<UninstallEntry> UninstallEntries() {
        var list = new List<UninstallEntry>();
        var roots = new[] {
            Tuple.Create(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
        };
        foreach (var r in roots)
            using (var root = r.Item1.OpenSubKey(r.Item2)) {
                if (root == null) continue;
                foreach (string name in root.GetSubKeyNames())
                    using (var k = root.OpenSubKey(name)) {
                        if (k == null) continue;
                        string folder = Convert.ToString(k.GetValue("InstallLocation"));
                        list.Add(new UninstallEntry {
                            Key = name,
                            Name = Convert.ToString(k.GetValue("DisplayName")).Trim(),
                            Publisher = Convert.ToString(k.GetValue("Publisher")),
                            Folder = String.IsNullOrWhiteSpace(folder) ? null : folder.Replace('/', '\\')
                        });
                    }
            }
        return list;
    }

    // Cheap fingerprint of launcher registrations, used to notice installs and uninstalls without a full scan.
    public static string RegistrySignature() {
        var parts = new List<string>();
        foreach (string key in new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games", @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", @"SOFTWARE\WOW6432Node\Rockstar Games" })
            using (var k = Registry.LocalMachine.OpenSubKey(key)) if (k != null) parts.Add(String.Join(",", k.GetSubKeyNames()));
        foreach (string key in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            using (var k = Registry.LocalMachine.OpenSubKey(key)) if (k != null) parts.Add(k.SubKeyCount.ToString());
        using (var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) if (k != null) parts.Add(k.SubKeyCount.ToString());
        return String.Join("|", parts);
    }

    // Folders whose manifests change when Steam or Epic install, update or remove a game.
    public static IEnumerable<Tuple<string, string>> ManifestFolders() {
        foreach (string library in SteamLibraries()) yield return Tuple.Create(library, "appmanifest_*.acf");
        string epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (Directory.Exists(epic)) yield return Tuple.Create(epic, "*.item");
    }
}
