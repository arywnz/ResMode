using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class LibraryGame {
    public string Key, Name;
    public Launcher Launcher;
    public bool Excluded, Installed, Competitive;
    public List<GameRule> Rules;
}

public static class GamePreferences {
    // One key per game: Steam by app id, everything else by launcher and name (a game may have several exes).
    public static string Key(GameRule rule) {
        var launcher = Launchers.Of(rule.Source);
        if (launcher == Launchers.Steam) return rule.Source.ToLowerInvariant();
        return launcher.Id + ":" + (rule.Name ?? "").Trim().ToLowerInvariant();
    }

    // Ignored when the user said so, or when it is a competitive game the user has not turned on.
    public static bool Excluded(Settings cfg, GameRule rule) {
        string key = Key(rule);
        if (cfg.ExcludedGames.Contains(key, StringComparer.OrdinalIgnoreCase)) return true;
        return cfg.SkipCompetitive && Competitive.Is(rule.Name) && !cfg.IncludedGames.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    public static void Exclude(Settings cfg, string key, bool exclude) {
        cfg.ExcludedGames.RemoveAll(x => String.Equals(x, key, StringComparison.OrdinalIgnoreCase));
        cfg.IncludedGames.RemoveAll(x => String.Equals(x, key, StringComparison.OrdinalIgnoreCase));
        if (exclude) cfg.ExcludedGames.Add(key);
        else cfg.IncludedGames.Add(key); // also overrides the competitive default
    }

    public static GameMode ModeOf(Settings cfg, string key) {
        return cfg.GameModes == null ? null : cfg.GameModes.FirstOrDefault(m => String.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    // width < 0 removes the game's own choice (back to the general resolution).
    public static void SetMode(Settings cfg, string key, int width, int height) {
        if (cfg.GameModes == null) cfg.GameModes = new List<GameMode>();
        cfg.GameModes.RemoveAll(m => String.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
        if (width >= 0) cfg.GameModes.Add(new GameMode { Key = key, Width = width, Height = height });
    }

    public static List<LibraryGame> Groups(Settings cfg) {
        return cfg.Games.GroupBy(Key, StringComparer.OrdinalIgnoreCase).Select(g => new LibraryGame {
            Key = g.Key,
            Name = g.First().Name,
            Launcher = Launchers.Of(g.First().Source),
            Excluded = Excluded(cfg, g.First()),
            Competitive = Competitive.Is(g.First().Name),
            Installed = g.Any(x => File.Exists(x.Exe)),
            Rules = g.ToList()
        }).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // Catalog results replace automatic entries; games the user added by hand are kept.
    public static void MergeCatalog(Settings cfg, IEnumerable<GameRule> automatic) {
        cfg.Games = cfg.Games.Where(g => g.Source == "Manual").Concat(automatic)
            .GroupBy(g => g.Exe, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    // Adds a game by hand with one or more executables. Returns how many new executables were added;
    // an executable that is already known just gets its game monitored again.
    public static int AddManual(Settings cfg, string name, IEnumerable<string> exes) {
        name = (name ?? "").Trim();
        if (name == "") throw new Exception("Dê um nome ao jogo.");
        int added = 0;
        foreach (string raw in exes) {
            string exe = Path.GetFullPath(raw);
            if (!File.Exists(exe) || !String.Equals(Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Arquivo não encontrado ou não é um .exe: " + exe);
            if (String.Equals(Path.GetFileName(exe), AppInfo.ExeName, StringComparison.OrdinalIgnoreCase))
                throw new Exception(AppInfo.Name + " não pode monitorar a si mesmo.");
            var existing = cfg.Games.FirstOrDefault(g => String.Equals(g.Exe, exe, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { Exclude(cfg, Key(existing), false); continue; }
            var rule = new GameRule { Name = name, Exe = exe, Source = "Manual" };
            cfg.Games.Add(rule);
            Exclude(cfg, Key(rule), false);
            added++;
        }
        return added;
    }

    public static void Tests(string report) {
        var cfg = new Settings();
        var a = new GameRule { Name = "Teste", Exe = @"C:\Games\Teste\game.exe", Source = "Steam:123" };
        var b = new GameRule { Name = "Teste", Exe = @"C:\Games\Teste\Bin\game-real.exe", Source = "Steam:123" };
        cfg.Games.Add(a);
        cfg.Games.Add(b);
        if (Groups(cfg).Count != 1) throw new Exception("Executáveis do mesmo jogo não foram agrupados.");
        Exclude(cfg, Key(a), true);
        MergeCatalog(cfg, new[] { a, b });
        if (!Excluded(cfg, a) || !Excluded(cfg, b)) throw new Exception("Atualização reativou jogo excluído.");

        var valorant = new GameRule { Name = "VALORANT", Exe = @"C:\Games\Riot\VALORANT.exe", Source = "Riot" };
        cfg.Games.Add(valorant);
        if (!Excluded(cfg, valorant)) throw new Exception("Jogo competitivo não foi ignorado por padrão.");
        Exclude(cfg, Key(valorant), false);
        if (Excluded(cfg, valorant)) throw new Exception("Jogo competitivo não voltou a ser usado.");
        cfg.Games.Remove(valorant);

        string root = Path.Combine(Path.GetTempPath(), AppInfo.Name + "-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string oldRoot = Store.Root;
        try {
            Store.Root = root;
            cfg.StartPaused = true;
            Store.SaveSettings(cfg);
            var read = Store.LoadSettings();
            if (!Excluded(read, b) || !read.StartPaused) throw new Exception("Exclusão ou pausa não persistiu.");
            Exclude(read, Key(a), false);
            if (Excluded(read, b)) throw new Exception("Reativação falhou.");
            MergeCatalog(read, new GameRule[0]);
            if (read.Games.Count != 0) throw new Exception("Jogo desinstalado não foi removido.");

            // A legacy jogos.xml without exclusions must not wipe the separate exclusions file.
            var saved = new Settings();
            saved.Games.Add(a);
            Exclude(saved, Key(a), true);
            Store.SaveSettings(saved);
            Store.Write(Store.Config, new Settings());
            var restarted = Store.LoadSettings();
            MergeCatalog(restarted, new[] { a, b });
            if (!Excluded(restarted, b)) throw new Exception("Reinício apagou exclusões.");

            string fixture = Path.Combine(root, "LibraryTestGame.exe");
            File.Copy(Application.ExecutablePath, fixture, true);
            var manual = new Settings();
            AddManual(manual, "Jogo de teste", new[] { fixture });
            if (AddManual(manual, "Jogo de teste", new[] { fixture }) != 0 || manual.Games.Count != 1) throw new Exception("Adição manual duplicou o jogo.");
            Exclude(manual, Key(manual.Games[0]), true);
            AddManual(manual, "Jogo de teste", new[] { fixture });
            if (Excluded(manual, manual.Games[0])) throw new Exception("Adicionar de novo não reativou o jogo.");
        } finally {
            Store.Root = oldRoot;
            try { Directory.Delete(root, true); } catch { }
        }

        if (!ShellRefresh.Ignorable("EdgeUiInputTopWndClass", 1905, 4) || ShellRefresh.Ignorable("#32770", 500, 300) || ShellRefresh.Ignorable("CabinetWClass", 700, 500))
            throw new Exception("Classificação de janelas do Explorer incorreta.");
        File.WriteAllText(report, "PASS: agrupamento, competitivos ignorados por padrão, exclusões persistentes, catálogo sem reativar removidos, remoção de desinstalados, adição manual sem duplicar e filtro de janelas do Explorer.");
    }
}

public static class StartupShortcut {
    public static string PathName { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AppInfo.Name + ".lnk"); } }
    public static bool Enabled { get { return File.Exists(PathName); } }

    public static void Set(bool enabled) {
        if (!enabled) {
            if (File.Exists(PathName)) File.Delete(PathName);
            return;
        }
        object com = null, shortcut = null;
        try {
            com = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic shell = com;
            shortcut = shell.CreateShortcut(PathName);
            dynamic link = shortcut;
            string exe = Application.ExecutablePath;
            link.TargetPath = exe;
            link.WorkingDirectory = Path.GetDirectoryName(exe);
            link.Arguments = "/background";
            link.IconLocation = exe + ",0";
            link.Description = AppInfo.Name + ": resolução automática nos jogos";
            link.Save();
        } finally {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (com != null) Marshal.FinalReleaseComObject(com);
        }
    }
}
