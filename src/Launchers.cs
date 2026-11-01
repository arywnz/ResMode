using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

// Game launchers: display name, how a GameRule's Source maps to one, and the launcher's own icon.
public class Launcher {
    public string Id, Name;
    Func<string> exe;
    Image icon;
    bool iconLoaded;

    public Launcher(string id, string name, Func<string> exe) { Id = id; Name = name; this.exe = exe; }

    public Image Icon {
        get {
            if (!iconLoaded) {
                iconLoaded = true;
                try {
                    string path = exe();
                    if (!String.IsNullOrEmpty(path) && File.Exists(path)) icon = Launchers.ExtractIcon(path, Theme.S(32));
                } catch { }
            }
            return icon;
        }
    }
}

public static class Launchers {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, int[] ids, uint count, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);

    // Icons come from the launcher's own files, found through the registry so it works wherever the launcher is installed.
    public static readonly Launcher Steam = new Launcher("steam", "Steam", () => First(Join(RegString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"), "steam.exe")));
    public static readonly Launcher Epic = new Launcher("epic", "Epic Games", () => First(UninstallIcon("Epic Games Launcher"), ProgramFilesX86(@"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe")));
    public static readonly Launcher Gog = new Launcher("gog", "GOG", () => First(Join(RegString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths", "client"), "GalaxyClient.exe"), UninstallIcon("GOG GALAXY")));
    public static readonly Launcher Ubisoft = new Launcher("ubisoft", "Ubisoft Connect", () => First(Join(RegString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Ubisoft\Launcher", "InstallDir"), "UbisoftConnect.exe"), UninstallIcon("Ubisoft Connect")));
    public static readonly Launcher Ea = new Launcher("ea", "EA app", () => First(UninstallIcon("EA app"), Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe")));
    public static readonly Launcher BattleNet = new Launcher("battle.net", "Battle.net", () => First(UninstallIcon("Battle.net"), ProgramFilesX86(@"Battle.net\Battle.net Launcher.exe")));
    public static readonly Launcher Riot = new Launcher("riot", "Riot Games", () => First(Join(Uninstall("Riot Client", "InstallLocation"), "RiotClientServices.exe"), UninstallIcon("Riot Client")));
    public static readonly Launcher Rockstar = new Launcher("rockstar", "Rockstar Games", () => First(Join(RegString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Rockstar Games\Launcher", "InstallFolder"), "Launcher.exe"), UninstallIcon("Rockstar Games Launcher")));
    public static readonly Launcher Manual = new Launcher("manual", "Adicionado por você", () => null);

    public static readonly Launcher[] All = { Steam, Epic, Gog, Ubisoft, Ea, BattleNet, Riot, Rockstar, Manual };

    // GameRule.Source is "Steam:<appid>" for Steam and the launcher name for the others ("Epic", "GOG", "Manual"...).
    public static Launcher Of(string source) {
        string id = (source ?? "").Split(':')[0].ToLowerInvariant();
        return All.FirstOrDefault(l => l.Id == id) ?? Manual;
    }

    public static string SourceName(Launcher l) {
        return l == Steam ? "Steam" : l == Epic ? "Epic" : l == Gog ? "GOG" : l == Ubisoft ? "Ubisoft" : l == Ea ? "EA" : l == BattleNet ? "Battle.net" : l == Riot ? "Riot" : l == Rockstar ? "Rockstar" : "Manual";
    }

    public static string RegString(RegistryKey hive, string key, string value) {
        using (var k = hive.OpenSubKey(key)) {
            string s = k == null ? null : Convert.ToString(k.GetValue(value));
            return String.IsNullOrWhiteSpace(s) ? null : s;
        }
    }

    static string ProgramFilesX86(string relative) { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), relative); }

    static string Join(string folder, string file) { return String.IsNullOrEmpty(folder) ? null : Path.Combine(folder, file); }

    static string First(params string[] candidates) { return candidates.FirstOrDefault(c => !String.IsNullOrEmpty(c) && File.Exists(c)); }

    // A value from the launcher's own uninstall entry, found by its display name.
    static string Uninstall(string displayName, string value) {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (string root in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                using (var k = hive.OpenSubKey(root)) {
                    if (k == null) continue;
                    foreach (string name in k.GetSubKeyNames())
                        using (var entry = k.OpenSubKey(name)) {
                            // Some launchers register names with stray spaces ("Riot Client ").
                            if (entry == null || !String.Equals(Convert.ToString(entry.GetValue("DisplayName")).Trim(), displayName, StringComparison.OrdinalIgnoreCase)) continue;
                            string s = Convert.ToString(entry.GetValue(value)).Trim().Trim('"');
                            if (s.Length > 0) return s.Replace('/', '\\');
                        }
                }
        return null;
    }

    // DisplayIcon is "C:\...\Launcher.exe,0" or an .ico.
    static string UninstallIcon(string displayName) {
        string icon = Uninstall(displayName, "DisplayIcon");
        if (icon == null) return null;
        int comma = icon.LastIndexOf(',');
        if (comma > 2 && !icon.Substring(comma).Contains("\\")) icon = icon.Substring(0, comma).Trim('"');
        return icon;
    }

    public static Image ExtractIcon(string file, int size) {
        var handles = new IntPtr[1];
        var ids = new int[1];
        if (PrivateExtractIcons(file, 0, size, size, handles, ids, 1, 0) == 0 || handles[0] == IntPtr.Zero) return null;
        try {
            using (var icon = System.Drawing.Icon.FromHandle(handles[0])) return icon.ToBitmap();
        } finally { DestroyIcon(handles[0]); }
    }
}
