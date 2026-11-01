using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

public class PlayRecord { public string Key; public long Seconds; public long LastPlayedTicks; }

// Time played per game. Steam games use Steam's own counters (they include play from before ResMode);
// every other game counts the sessions ResMode sees, saved when the game closes.
public static class Playtime {
    public class Info { public TimeSpan Total; public DateTime? Last; public bool Running; }

    static List<PlayRecord> records;
    static readonly Dictionary<string, DateTime> open = new Dictionary<string, DateTime>();
    static string FilePath { get { return Path.Combine(Store.Root, "tempo-de-jogo.xml"); } }

    static List<PlayRecord> Records {
        get {
            if (records == null) {
                try { records = File.Exists(FilePath) ? Store.Read<List<PlayRecord>>(FilePath) : new List<PlayRecord>(); }
                catch { records = new List<PlayRecord>(); }
            }
            return records;
        }
    }

    // Called every detection tick with the keys of the games running now.
    public static void Update(IEnumerable<string> runningKeys) {
        var now = DateTime.UtcNow;
        var running = new HashSet<string>(runningKeys);
        foreach (string key in running) if (!open.ContainsKey(key)) open[key] = now;
        var closed = open.Keys.Where(k => !running.Contains(k)).ToList();
        if (closed.Count == 0) return;
        foreach (string key in closed) {
            var r = Records.FirstOrDefault(x => x.Key == key);
            if (r == null) Records.Add(r = new PlayRecord { Key = key });
            r.Seconds += (long)(now - open[key]).TotalSeconds;
            r.LastPlayedTicks = now.Ticks;
            open.Remove(key);
        }
        try { Store.Write(FilePath, Records); } catch (Exception e) { Store.Log("Não deu para salvar o tempo de jogo: " + e.Message, LogKind.Error); }
    }

    public static Info Get(string key) {
        var info = new Info();
        var own = Records.FirstOrDefault(x => x.Key == key);
        if (own != null) {
            info.Total = TimeSpan.FromSeconds(own.Seconds);
            if (own.LastPlayedTicks > 0) info.Last = new DateTime(own.LastPlayedTicks, DateTimeKind.Utc).ToLocalTime();
        }
        if (key.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)) {
            long minutes, lastUnix;
            if (SteamStats(key.Substring(6), out minutes, out lastUnix)) {
                if (TimeSpan.FromMinutes(minutes) > info.Total) info.Total = TimeSpan.FromMinutes(minutes);
                if (lastUnix > 0) {
                    var last = DateTimeOffset.FromUnixTimeSeconds(lastUnix).LocalDateTime;
                    if (info.Last == null || last > info.Last) info.Last = last;
                }
            }
        }
        DateTime started;
        if (open.TryGetValue(key, out started)) { info.Running = true; info.Total += DateTime.UtcNow - started; }
        return info;
    }

    public static string FormatTotal(TimeSpan t) {
        if (t.TotalMinutes < 1) return "Nenhum";
        if (t.TotalHours < 1) return (int)t.TotalMinutes + " min";
        int h = (int)t.TotalHours, m = t.Minutes;
        return h >= 100 || m == 0 ? h.ToString("N0", new CultureInfo("pt-BR")) + " h" : h + " h " + m + " min";
    }

    public static string FormatLast(Info info) {
        if (info.Running) return "Agora";
        if (info.Last == null) return "Nunca";
        var days = (DateTime.Today - info.Last.Value.Date).Days;
        if (days <= 0) return "Hoje";
        if (days == 1) return "Ontem";
        if (days < 7) return "Há " + days + " dias";
        if (days < 30) return days < 14 ? "Há 1 semana" : "Há " + days / 7 + " semanas";
        return info.Last.Value.ToString(info.Last.Value.Year == DateTime.Today.Year ? "d 'de' MMM" : "MMM 'de' yyyy", new CultureInfo("pt-BR"));
    }

    // Steam keeps "Playtime" (minutes) and "LastPlayed" (unix time) per app in the logged-in user's localconfig.vdf.
    static Dictionary<string, Tuple<long, long>> steam;
    static string steamStamp;

    static bool SteamStats(string appId, out long minutes, out long lastUnix) {
        minutes = lastUnix = 0;
        try {
            string file = LocalConfig();
            if (file == null) return false;
            var info = new FileInfo(file);
            string stamp = file + info.LastWriteTimeUtc.Ticks;
            if (stamp != steamStamp && info.Length < 32 * 1024 * 1024) { steam = ParseApps(File.ReadAllText(file)); steamStamp = stamp; }
            Tuple<long, long> v;
            if (steam == null || !steam.TryGetValue(appId, out v)) return false;
            minutes = v.Item1;
            lastUnix = v.Item2;
            return true;
        } catch { return false; }
    }

    static string LocalConfig() {
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) {
            string root = k == null ? null : Convert.ToString(k.GetValue("SteamPath"));
            if (String.IsNullOrEmpty(root)) return null;
            string users = Path.Combine(root, "userdata");
            if (!Directory.Exists(users)) return null;
            // The most recently written profile is the account that is (or was last) logged in.
            return Directory.GetDirectories(users).Select(d => Path.Combine(d, "config", "localconfig.vdf")).Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
    }

    static Dictionary<string, Tuple<long, long>> ParseApps(string text) {
        var result = new Dictionary<string, Tuple<long, long>>();
        int apps = text.IndexOf("\"apps\"", StringComparison.OrdinalIgnoreCase);
        if (apps < 0) return result;
        foreach (Match m in new Regex("\"(\\d+)\"\\s*\\{").Matches(text, apps)) {
            // Read the app's block up to its matching brace; nested blocks (cloud sync, etc.) are skipped over.
            int depth = 1, i = m.Index + m.Length;
            while (i < text.Length && depth > 0) { if (text[i] == '{') depth++; else if (text[i] == '}') depth--; i++; }
            string block = text.Substring(m.Index + m.Length, i - m.Index - m.Length);
            var play = Regex.Match(block, "\"Playtime\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase);
            var last = Regex.Match(block, "\"LastPlayed\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase);
            if (!play.Success && !last.Success) continue;
            result[m.Groups[1].Value] = Tuple.Create(play.Success ? Int64.Parse(play.Groups[1].Value) : 0, last.Success ? Int64.Parse(last.Groups[1].Value) : 0);
        }
        return result;
    }
}
