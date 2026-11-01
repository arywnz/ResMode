using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

public enum LogKind { Game, Library, System, Error }

public static class Store {
    const long MaxLogBytes = 1000000;
    const int KeptOldLogs = 2;

    public static string Root = AppDomain.CurrentDomain.BaseDirectory;
    public static string Config { get { return Path.Combine(Root, "jogos.xml"); } }
    public static string Exclusions { get { return Path.Combine(Root, "exclusoes.xml"); } }
    public static string State { get { return Path.Combine(Root, "restauracao.xml"); } }
    public static string LogFile { get { return Path.Combine(Root, AppInfo.LogName); } }
    public static string IconFile { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppInfo.IconName); } }

    public static void UseUserData() {
        string besideExe = AppDomain.CurrentDomain.BaseDirectory;
        Root = File.Exists(Path.Combine(besideExe, "jogos.xml")) ? besideExe : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name);
        Directory.CreateDirectory(Root);
    }

    public static Settings LoadSettings() {
        var cfg = File.Exists(Config) ? Read<Settings>(Config) : new Settings();
        if (cfg.ExcludedGames == null) cfg.ExcludedGames = new List<string>();
        if (cfg.IncludedGames == null) cfg.IncludedGames = new List<string>();
        if (File.Exists(Exclusions)) cfg.ExcludedGames = Read<List<string>>(Exclusions);
        return cfg;
    }

    public static void SaveSettings(Settings cfg) {
        Write(Exclusions, cfg.ExcludedGames);
        Write(Config, cfg);
    }

    // What gets written: "all", "games" (game sessions only) or "off". Errors are always kept, to help when something fails.
    public static string LogLevel = "all";
    static readonly HashSet<string> migratedLogs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    static readonly string[] Tags = { "jogo", "biblioteca", "sistema", "erro" };
    public static string Tag(LogKind kind) { return Tags[(int)kind]; }

    public static void Log(string message, LogKind kind = LogKind.System) {
        if (kind != LogKind.Error && (LogLevel == "off" || (LogLevel == "games" && kind != LogKind.Game))) return;
        try {
            LocalData.WithFileLock(LogFile,delegate{
            if(!migratedLogs.Contains(Root)){
                foreach(string file in Directory.GetFiles(Root,AppInfo.LogName+"*"))LocalData.MigrateLog(file);
                migratedLogs.Add(Root);
            }
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxLogBytes) {
                File.Move(LogFile, LogFile + "." + DateTime.Now.ToString("yyyyMMddHHmmss"));
                var old = Directory.GetFiles(Root, AppInfo.LogName + ".*").OrderByDescending(f => f).Skip(KeptOldLogs);
                foreach (string f in old) File.Delete(f);
            }
            // One entry per line: names from manifests or the registry cannot forge extra log lines.
            string line = (message ?? "").Replace("\r", " ").Replace("\n", " ");
            File.AppendAllText(LogFile,LocalData.ProtectLog(DateTime.Now.ToString("s")+" ["+Tag(kind)+"] "+line)+Environment.NewLine,Encoding.UTF8);
            });
        } catch { }
    }

    public static IEnumerable<string> ReadLogLines(string file){
        if(new FileInfo(file).Length>2*1024*1024)throw new InvalidDataException("Histórico local grande demais.");
        foreach(string line in File.ReadLines(file,Encoding.UTF8)){
            string value;
            try{value=LocalData.ReadLog(line);}catch{value=DateTime.Now.ToString("s")+" [erro] Não foi possível ler um registro protegido.";}
            yield return value;
        }
    }

    // DTDs and external entities are refused, and files are size-capped, so a tampered file cannot expand or fetch anything.
    public static T Read<T>(string file) {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersFromEntities = 0 };
        using (var s = new MemoryStream(LocalData.ReadXmlBytes(file))) using (var reader = XmlReader.Create(s, settings))
            return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
    }

    public static void Write<T>(string file, T value) {
        using(var memory=new MemoryStream()){
            new XmlSerializer(typeof(T)).Serialize(memory,value);
            LocalData.WriteXmlBytes(file,memory.ToArray());
        }
    }

    // Returns the display to the mode saved before the game resolution was applied.
    public static bool Restore(int expectedOwner = 0) {
        if (!File.Exists(State)) return true;
        try {
            var r = Read<Recovery>(State);
            if (expectedOwner != 0 && r.OwnerPid != expectedOwner) return true;
            DesktopLayout.StopTracking();
            var m = Native.Find(r.Device, r.Width, r.Height, r.Hz);
            var currentWindows = DesktopLayout.Capture(r.Device, true, DesktopLayout.GamePids(LoadSettings().Games));
            var windows = DesktopLayout.RecoveryWindows(r.Windows, currentWindows);
            int rc = Native.Set(r.Device, m, false);
            // Monitors that were moved beside the game's go back to where they were.
            if (rc == 0 && r.Others != null)
                foreach (var o in r.Others) {
                    try { if (Native.SetPosition(o.Device, o.X, o.Y) != 0) Log("Não deu para devolver o monitor " + o.Device + " ao lugar.", LogKind.Error); }
                    catch (Exception e) { Log("Erro ao devolver o monitor " + o.Device + ": " + e.Message, LogKind.Error); }
                }
            var now = Native.Current(r.Device);
            bool ok = rc == 0 && now.width == r.Width && now.height == r.Height;
            Log(ok ? "Resolução normal de volta: " + r.Width + " × " + r.Height + " a " + m.hz + " Hz."
                   : "O monitor não confirmou a volta para " + r.Width + " × " + r.Height + " (código " + rc + ").", ok ? LogKind.Game : LogKind.Error);
            if (ok) {
                DesktopLayout.SaveDesktopRecovery(r.Device, r.Width, r.Height, windows);
                File.Delete(State);
                try {
                    DesktopLayout.ResumeDesktopRestore();
                    if (LoadSettings().AutoRefreshTaskbar) ShellRefresh.Request();
                } catch (Exception e) { Log("Erro depois de voltar a resolução: " + e.Message, LogKind.Error); }
            }
            else if (System.Windows.Forms.Application.MessageLoop) DesktopLayout.Track(r.Windows, r.OwnerPid);
            return ok;
        } catch (Exception e) {
            Log("Não deu para voltar a resolução: " + e.Message, LogKind.Error);
            return false;
        }
    }
}
