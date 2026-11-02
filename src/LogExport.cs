using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// "Gerar log": the history is stored protected for the current Windows account, so this writes it out
// as one plain .txt (UTF-8 with BOM, so Notepad shows the accents) that can be opened, copied or sent.
public static class LogExport {
    const int MaxLines = 5000;

    public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppInfo.Name); } }
    public static string FilePath { get { return Path.Combine(Folder, AppInfo.Name + "-log.txt"); } }

    // Same file name every time, so exports never pile up.
    public static string Create(Settings cfg, string device) {
        var text = new StringBuilder();
        text.AppendLine("ResMode - registro de atividade");
        text.AppendLine("Gerado em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
        text.AppendLine();
        text.AppendLine("Windows:       " + WindowsName());
        text.AppendLine("Vídeo:         " + VideoAdapters());
        try {
            var m = Native.Current(device);
            text.AppendLine("Monitor:       " + device + ", " + m.width + " × " + m.height + " a " + m.hz + " Hz");
        } catch { text.AppendLine("Monitor:       " + device); }
        text.AppendLine("Desktop:       " + cfg.DesktopWidth + " × " + cfg.DesktopHeight);
        text.AppendLine("Resolução:     " + (cfg.Width > 0 ? cfg.Width + " × " + cfg.Height : "não escolhida"));
        text.AppendLine("Jogos:         " + cfg.Games.Select(g => GamePreferences.Key(g)).Distinct().Count());
        text.AppendLine("Registro:      " + (cfg.LogLevel == "off" ? "só erros" : cfg.LogLevel == "games" ? "só os jogos" : "tudo"));
        text.AppendLine(new string('-', 64));

        var lines = new List<string>();
        var files = Directory.GetFiles(Store.Root, AppInfo.LogName + "*").Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => String.Equals(f, Store.LogFile, StringComparison.OrdinalIgnoreCase) ? "~" : f);
        foreach (string file in files) {
            try { lines.AddRange(Store.ReadLogLines(file).Select(Pretty).Where(l => l != null)); } catch { }
        }
        if (lines.Count == 0) text.AppendLine("Nenhum registro ainda.");
        else foreach (string line in lines.Skip(Math.Max(0, lines.Count - MaxLines))) text.AppendLine(Anonymize(line));

        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, text.ToString(), new UTF8Encoding(true));
        return FilePath;
    }

    // Environment.OSVersion reports 6.2 for apps without a compatibility manifest, so ask the registry.
    static string WindowsName() {
        try {
            using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
                int build = Int32.Parse(Convert.ToString(k.GetValue("CurrentBuildNumber")));
                string version = Convert.ToString(k.GetValue("DisplayVersion")), revision = Convert.ToString(k.GetValue("UBR"));
                return (build >= 22000 ? "Windows 11" : "Windows 10") + (version != "" ? " " + version : "") + " (build " + build + (revision != "" ? "." + revision : "") + ")";
            }
        } catch { return Environment.OSVersion.VersionString; }
    }

    // Graphics adapters and driver versions, as Windows lists them. Display problems almost always trace back to these.
    static string VideoAdapters() {
        var found = new List<string>();
        try {
            using (var cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                foreach (string name in cls.GetSubKeyNames().Where(n => n.Length == 4 && n.All(Char.IsDigit)))
                    using (var k = cls.OpenSubKey(name)) {
                        string desc = k == null ? null : Convert.ToString(k.GetValue("DriverDesc"));
                        if (!String.IsNullOrEmpty(desc)) found.Add(desc + " (driver " + Convert.ToString(k.GetValue("DriverVersion")) + ")");
                    }
        } catch { }
        return found.Count == 0 ? "não encontrado" : String.Join("; ", found);
    }

    // "2026-10-31T20:46:11 [jogo] Jogo aberto: X" becomes "2026-10-31 20:46:11  [jogo]       Jogo aberto: X".
    static string Pretty(string line) {
        DateTime t;
        if (line == null || line.Length < 21 || !DateTime.TryParseExact(line.Substring(0, 19), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return null;
        string rest = line.Substring(20), tag = "[sistema]";
        if (rest.StartsWith("[")) {
            int end = rest.IndexOf(']');
            if (end > 0) { tag = rest.Substring(0, end + 1); rest = rest.Substring(end + 1).TrimStart(); }
        }
        return t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + tag.PadRight(12) + rest;
    }

    // The file is meant to be shared, so what identifies the person or the PC stays out of it: the user name, the profile
    // folder (and any other account's folder under Users), the computer name, e-mail addresses and Windows account ids.
    static string Anonymize(string line) {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!String.IsNullOrEmpty(profile)) line = Regex.Replace(line, Regex.Escape(profile), "%USERPROFILE%", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"(?i)([A-Z]:\\Users\\)[^\\/\s""'<>|]+", "$1usuário");
        if (!String.IsNullOrEmpty(Environment.UserName) && Environment.UserName.Length > 2)
            line = Regex.Replace(line, Regex.Escape(Environment.UserName), "usuário", RegexOptions.IgnoreCase);
        if (!String.IsNullOrEmpty(Environment.MachineName) && Environment.MachineName.Length > 2)
            line = Regex.Replace(line, Regex.Escape(Environment.MachineName), "computador", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", "<e-mail>");
        line = Regex.Replace(line, @"S-1-5-21(-\d+){3,5}", "<id-da-conta>");
        return line;
    }

    // Public for the tests: the same scrubbing, on any text.
    public static string Scrub(string text) { return Anonymize(text); }

    // Opens the folder with the file already selected, ready to copy or send.
    public static void Reveal(string file) {
        string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        Process.Start(explorer, "/select,\"" + file + "\"");
    }
}
