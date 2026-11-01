using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

public class AppContext : ApplicationContext {
    // Polling is cheap (a PID list), and slower while a game runs since only its exit matters then.
    const int IdleInterval = 1000, GameInterval = 2000;
    const int RestoreDelaySeconds = 3;

    Settings config;
    NotifyIcon tray;
    System.Windows.Forms.Timer timer;
    bool paused, active, busy;
    string lastGames = "", lastError = "";
    DateTime emptySince = DateTime.MinValue;
    bool existingSession, attemptedSession;
    bool observedGames;
    DateTime gameExitSince = DateTime.MinValue;

    // Full executable path -> game name.
    Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    // PID -> game name ("" when the PID is not a monitored game), so each process path is queried once.
    Dictionary<int, string> knownPids = new Dictionary<int, string>();
    DateTime pidCacheReset = DateTime.MinValue;

    ToolStripMenuItem status, pause;
    Dashboard dashboard;
    EventWaitHandle showRequest;
    Task<List<GameRule>> catalogTask;
    DateTime nextCatalog = DateTime.Now.AddSeconds(10);
    bool forceCatalog = true; // one quick scan after startup picks up anything installed while the app was closed
    DateTime nextShellCheck = DateTime.MinValue;
    readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
    DateTime libraryChangedAt = DateTime.MinValue; // written by watcher threads; a 64-bit write is atomic on x64
    string registrySignature, watchedFolders;
    DateTime nextRegistryCheck = DateTime.MinValue;

    public bool Paused { get { return paused; } }
    public bool DisplayActive { get { return active; } }
    public Settings Configuration { get { return config; } }
    public int LibraryVersion { get; private set; }
    public List<string> RunningNames { get; private set; }
    public bool CatalogBusy { get { return catalogTask != null; } }
    public bool CatalogPending { get { return catalogTask != null || forceCatalog; } }
    public string DisplayDevice { get { return Device(); } }
    public string DesktopMode { get { return config.DesktopWidth + " × " + config.DesktopHeight; } }

    public string ErrorText { get { return lastError; } }
    public bool WaitingNextSession { get { return existingSession; } }

    public string CurrentMode {
        get {
            try {
                var m = Native.Current(Device());
                return m.width + " × " + m.height + " a " + m.hz + " Hz";
            } catch { return "monitor indisponível"; }
        }
    }

    public AppContext(bool showWindow) {
        RunningNames = new List<string>();
        LibraryVersion = 1;
        config = Store.LoadSettings();
        paused = config.StartPaused;
        Store.LogLevel = config.LogLevel ?? "all";
        if (!File.Exists(Store.State)) FillDisplayDefaults(false);
        Index();
        showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.ShowEventName);
        BuildTray();

        active = File.Exists(Store.State);
        existingSession = !active && Running().Count > 0;
        if (existingSession) Store.Log("Um jogo já estava aberto. A troca fica para a próxima vez.", LogKind.Game);

        timer = new System.Windows.Forms.Timer { Interval = IdleInterval };
        timer.Tick += delegate { Tick(); };
        timer.Start();
        SystemEvents.SessionEnding += SessionEnding;
        Store.Log(AppInfo.Name + " aberto.");
        WatchLibraries();
        CheckCompetitiveNotice();
        Tick();
        if (showWindow) ShowDashboard();
        else Native.TrimMemory();
    }

    // The tray keeps only quick actions; everything else lives in the window.
    void BuildTray() {
        tray = new NotifyIcon { Icon = new Icon(Store.IconFile), Text = AppInfo.Name };
        var menu = Theme.Menu();
        status = new ToolStripMenuItem("") { Enabled = false };
        menu.Items.Add(status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Abrir " + AppInfo.Name, null, delegate { ShowDashboard(); });
        pause = new ToolStripMenuItem("Pausar", null, delegate { SetPaused(!paused); });
        menu.Items.Add(pause);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, delegate {
            try { Quit(); } catch (Exception e) { MessageBox.Show(e.Message, AppInfo.Name); }
        });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { ShowDashboard(); };
        tray.Visible = true;
    }

    // The chosen monitor, or the primary one if it is unset or no longer connected.
    string Device() {
        if (!String.IsNullOrWhiteSpace(config.Display) && Screen.AllScreens.Any(s => s.DeviceName == config.Display)) return config.Display;
        return Screen.PrimaryScreen.DeviceName;
    }

    void Index() {
        paths.Clear();
        knownPids.Clear();
        foreach (var g in config.Games.Where(g => !GamePreferences.Excluded(config, g))) paths[g.Exe] = g.Name;
    }

    // Lists PIDs each tick and queries the image path only for PIDs not seen before.
    List<string> Running() {
        if (DateTime.Now >= pidCacheReset) {
            // Periodic full refresh guards against a PID being reused by a different process.
            knownPids.Clear();
            pidCacheReset = DateTime.Now.AddMinutes(1);
        }
        var found = new List<string>();
        var alive = new HashSet<int>();
        foreach (int pid in Native.ProcessIds()) {
            alive.Add(pid);
            string name;
            if (!knownPids.TryGetValue(pid, out name)) {
                if (!paths.TryGetValue(Native.ExePath(pid), out name)) name = "";
                knownPids[pid] = name;
            }
            if (name != "" && !found.Contains(name)) found.Add(name);
        }
        if (knownPids.Count > alive.Count)
            foreach (int pid in knownPids.Keys.Where(k => !alive.Contains(k)).ToList()) knownPids.Remove(pid);
        return found;
    }

    void Tick() {
        if (busy) return;
        busy = true;
        try {
            if (showRequest.WaitOne(0)) ShowDashboard();
            var games = Running();
            RunningNames = games;
            Playtime.Update(games.Select(n => config.Games.FirstOrDefault(g => g.Name == n)).Where(g => g != null).Select(GamePreferences.Key).Distinct());
            string text = String.Join(", ", games);

            if (games.Count > 0) {
                observedGames = true;
                gameExitSince = DateTime.MinValue;
            } else if (observedGames) {
                if (gameExitSince == DateTime.MinValue) gameExitSince = DateTime.Now;
                if (!active && (DateTime.Now - gameExitSince).TotalSeconds >= RestoreDelaySeconds) {
                    observedGames = false;
                    if (config.AutoRefreshTaskbar) ShellRefresh.Request();
                }
            }

            if (games.Count > 0 && !paused) {
                emptySince = DateTime.MinValue;
                if (text != lastGames) {
                    Store.Log("Jogo aberto: " + text, LogKind.Game);
                    lastGames = text;
                }
                if (!active && !existingSession && !attemptedSession) {
                    attemptedSession = true;
                    try {
                        Apply();
                        lastError = "";
                    } catch (Exception e) {
                        Store.Log(e.Message, LogKind.Error);
                        if (lastError != e.Message) {
                            tray.ShowBalloonTip(7000, "Resolução de jogo indisponível", e.Message, ToolTipIcon.None);
                            Notify("Resolução indisponível", e.Message, LogKind.Error);
                            lastError = e.Message;
                        }
                    }
                }
            } else if (active && games.Count == 0) {
                if (emptySince == DateTime.MinValue) emptySince = DateTime.Now;
                if ((DateTime.Now - emptySince).TotalSeconds >= RestoreDelaySeconds) Restore();
            }

            if (games.Count == 0) {
                lastGames = "";
                lastError = "";
                existingSession = false;
                if (!active) attemptedSession = false;
            }

            CheckLibraryChanges();
            if (catalogTask != null && catalogTask.IsCompleted) {
                if (catalogTask.Status == TaskStatus.RanToCompletion) {
                    string before = LibrarySignature();
                    var oldNames = new HashSet<string>(config.Games.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
                    GamePreferences.MergeCatalog(config, catalogTask.Result);
                    if (LibrarySignature() != before) {
                        SaveConfiguration();
                        var newNames = new HashSet<string>(config.Games.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
                        ReportLibraryChange(newNames.Where(n => !oldNames.Contains(n)).ToList(), oldNames.Where(n => !newNames.Contains(n)).ToList());
                        CheckCompetitiveNotice();
                    }
                } else Store.Log("Atualização da biblioteca adiada: o PC voltou a ser usado ou a Steam não respondeu.", LogKind.Library);
                catalogTask = null;
                nextCatalog = DateTime.Now.AddMinutes(5);
                if (dashboard == null || dashboard.IsDisposed) Native.TrimMemory();
            }
            if (catalogTask == null && games.Count == 0 && DateTime.Now >= nextCatalog && (forceCatalog || Native.IdleMs() >= 45000)) {
                bool force = forceCatalog;
                forceCatalog = false;
                catalogTask = Task.Factory.StartNew(() => Catalog.Scan(force));
            }

            if (games.Count == 0 && !active && ShellRefresh.IsPending && DateTime.Now >= nextShellCheck) {
                nextShellCheck = DateTime.Now.AddSeconds(10);
                ShellRefresh.TryRun(Device());
            }

            if (games.Count == 0 && !active) DesktopLayout.ResumeDesktopRestore();
            timer.Interval = games.Count > 0 && (active || paused || existingSession) ? GameInterval : IdleInterval;
            UpdateStatus();
        } catch (Exception e) {
            Store.Log("Erro na detecção: " + e.Message, LogKind.Error);
        } finally {
            busy = false;
        }
    }

    // Live library: Steam/Epic manifests are watched on disk, other launchers through a cheap registry fingerprint.
    // A change triggers a catalog scan a few seconds later (debounced, since downloads touch manifests often).
    void WatchLibraries() {
        foreach (var w in watchers) w.Dispose();
        watchers.Clear();
        var folders = Catalog.ManifestFolders().ToList();
        watchedFolders = String.Join("|", folders.Select(f => f.Item1));
        foreach (var folder in folders) {
            try {
                var w = new FileSystemWatcher(folder.Item1, folder.Item2) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
                FileSystemEventHandler changed = (s, e) => libraryChangedAt = DateTime.Now;
                w.Created += changed;
                w.Changed += changed;
                w.Deleted += changed;
                w.Renamed += (s, e) => libraryChangedAt = DateTime.Now;
                w.EnableRaisingEvents = true;
                watchers.Add(w);
            } catch (Exception e) { Store.Log("Não deu para acompanhar " + folder.Item1 + ": " + e.Message, LogKind.Error); }
        }
        registrySignature = Catalog.RegistrySignature();
    }

    void CheckLibraryChanges() {
        if (DateTime.Now >= nextRegistryCheck) {
            nextRegistryCheck = DateTime.Now.AddSeconds(30);
            string signature = Catalog.RegistrySignature();
            if (signature != registrySignature) libraryChangedAt = DateTime.Now;
            registrySignature = signature;
            // A Steam library added on another drive (or Epic installed) after startup gets watched too.
            if (String.Join("|", Catalog.ManifestFolders().Select(f => f.Item1)) != watchedFolders) {
                WatchLibraries();
                libraryChangedAt = DateTime.Now;
            }
        }
        if (libraryChangedAt != DateTime.MinValue && (DateTime.Now - libraryChangedAt).TotalSeconds >= 6 && catalogTask == null) {
            libraryChangedAt = DateTime.MinValue;
            RefreshCatalog();
        }
    }

    void ReportLibraryChange(List<string> added, List<string> removed) {
        foreach (string n in added) Store.Log("Novo jogo na biblioteca: " + n + ".", LogKind.Library);
        foreach (string n in removed) Store.Log("Saiu da biblioteca: " + n + ".", LogKind.Library);
        if (added.Count > 0) Notify(added.Count == 1 ? "Jogo novo na biblioteca" : added.Count + " jogos novos na biblioteca", String.Join(", ", added), LogKind.Library);
        if (removed.Count > 0) Notify(removed.Count == 1 ? "Jogo removido" : removed.Count + " jogos removidos", String.Join(", ", removed) + (removed.Count == 1 ? " foi desinstalado." : " foram desinstalados."), LogKind.Library);
    }

    // In-app notifications shown under the bell; kept for this session only.
    public class Notice { public DateTime Time; public string Title, Text; public LogKind Kind; }
    public readonly List<Notice> Notices = new List<Notice>();
    bool restoreFailed;
    public int Unread { get; private set; }

    public void Notify(string title, string text, LogKind kind) {
        Notices.Insert(0, new Notice { Time = DateTime.Now, Title = title, Text = text, Kind = kind });
        if (Notices.Count > 50) Notices.RemoveAt(Notices.Count - 1);
        Unread++;
    }

    public void MarkNoticesRead() { Unread = 0; }
    public void ClearNotices() { Notices.Clear(); Unread = 0; }

    // Once: tells the user which competitive games stay at the desktop resolution.
    void CheckCompetitiveNotice() {
        if (config.CompetitiveNotified || !config.SkipCompetitive) return;
        var names = config.Games.Where(g => Competitive.Is(g.Name)).Select(g => g.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return;
        Notify("Jogos competitivos ficam de fora", String.Join(", ", names) + (names.Count == 1 ? " continua" : " continuam") + " na resolução normal, para não perder FPS. Dá para mudar nas configurações.", LogKind.Library);
        config.CompetitiveNotified = true;
        Store.SaveSettings(config);
    }

    public void SetLogLevel(string level) {
        config.LogLevel = level;
        Store.LogLevel = level;
        SaveConfiguration();
    }

    public void ClearLog() {
        foreach (string f in Directory.GetFiles(Store.Root, AppInfo.LogName + "*")) File.Delete(f);
    }

    string LibrarySignature() { return String.Join("|", config.Games.Select(g => g.Exe).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)); }

    // Fills in what depends on this PC: the desktop mode and, when none was chosen (or the monitor changed),
    // a game resolution the driver actually offers. Called only while the desktop is at its normal mode.
    void FillDisplayDefaults(bool monitorChanged) {
        try {
            var current = Native.Current(Device());
            bool changed = false;
            if (monitorChanged || config.DesktopWidth == 0 || config.DesktopHeight == 0) {
                config.DesktopWidth = current.width;
                config.DesktopHeight = current.height;
                changed = true;
            }
            bool valid = config.Width > 0 && Native.LargerModes(Device(), config.DesktopWidth, config.DesktopHeight).Any(m => m.width == config.Width && m.height == config.Height);
            if ((config.Width == 0 || (monitorChanged && !valid))) {
                int w, h;
                Native.PickGameMode(Device(), config.DesktopWidth, config.DesktopHeight, out w, out h);
                config.Width = w;
                config.Height = h;
                changed = true;
                Store.Log(w > 0 ? "Resolução de jogo escolhida: " + w + " × " + h + "." : "Nenhuma resolução maior que o desktop disponível neste monitor.");
            }
            if (changed) Store.SaveSettings(config);
        } catch (Exception e) { Store.Log("Erro ao ler o monitor: " + e.Message, LogKind.Error); }
    }

    void Apply() {
        if (config.Width == 0) throw new Exception("Nenhuma resolução maior que a do desktop está disponível. " + Native.EnableHint);
        string device = Device();
        var current = Native.Current(device);
        var target = Native.Find(device, config.Width, config.Height, current.hz);
        int test = Native.Set(device, target, true);
        if (test != 0) throw new Exception("O driver recusou " + config.Width + " × " + config.Height + ". Nada foi alterado.");

        // Return to the desktop mode in use now; if it is already the game mode, fall back to the last known desktop mode.
        int deskW = current.width, deskH = current.height;
        if (deskW == config.Width && deskH == config.Height) {
            deskW = config.DesktopWidth;
            deskH = config.DesktopHeight;
        } else if (deskW != config.DesktopWidth || deskH != config.DesktopHeight) {
            config.DesktopWidth = deskW;
            config.DesktopHeight = deskH;
            Store.SaveSettings(config);
        }
        var desktop = Native.Find(device, deskW, deskH, current.hz);
        DesktopLayout.CancelDesktopRestore();
        var windows = DesktopLayout.Capture(device, true, DesktopLayout.GamePids(config.Games));
        int ownerPid = Process.GetCurrentProcess().Id;
        Store.Write(Store.State, new Recovery { Device = device, Width = deskW, Height = deskH, Hz = desktop.hz, Bits = desktop.bits, OwnerPid = ownerPid, Windows = windows });
        active = true;
        DesktopLayout.Track(windows, ownerPid);
        int rc = Native.Set(device, target, false);
        var after = Native.Current(device);
        if (rc != 0 || after.width != config.Width || after.height != config.Height) {
            Store.Restore();
            active = File.Exists(Store.State);
            throw new Exception("O monitor não aceitou " + config.Width + " × " + config.Height + ".");
        }
        DesktopLayout.Settle(windows.Where(w => !String.Equals(w.Device, device, StringComparison.OrdinalIgnoreCase)).ToList());
        Store.Log("Resolução de jogo ligada: " + after.width + " × " + after.height + " a " + after.hz + " Hz.", LogKind.Game);
        // No icon: Windows then shows only the app's own icon in the header, instead of a generic blue "i".
        string game = RunningNames.FirstOrDefault();
        tray.ShowBalloonTip(2500, (game ?? "Jogo") + " em " + after.width + " × " + after.height,
            after.hz + " Hz. A tela volta para " + deskW + " × " + deskH + " quando o jogo fechar.", ToolTipIcon.None);
    }

    bool Restore() {
        bool wasActive = active;
        bool ok = Store.Restore();
        active = File.Exists(Store.State);
        if (!ok && !restoreFailed) Notify("A resolução não voltou ao normal", "O monitor não aceitou a troca. O app continua tentando; veja a aba Atividade.", LogKind.Error);
        restoreFailed = !ok;
        if (ok) {
            emptySince = DateTime.MinValue;
            if (wasActive && !active) {
                observedGames = false;
                gameExitSince = DateTime.MinValue;
            }
        }
        return ok;
    }

    void UpdateStatus() {
        string text = paused ? "Pausado"
            : active ? "Em jogo em " + config.Width + " × " + config.Height
            : lastError != "" ? "Resolução indisponível"
            : "Esperando um jogo";
        status.Text = text;
        pause.Text = paused ? "Retomar" : "Pausar";
        tray.Text = AppInfo.Name + ": " + text.ToLowerInvariant();
    }

    void SaveConfiguration() {
        Store.SaveSettings(config);
        Index();
        LibraryVersion++;
        UpdateStatus();
    }

    public void ShowDashboard() {
        if (dashboard == null || dashboard.IsDisposed) {
            dashboard = new Dashboard(this);
            // The window is rebuilt on demand; closing it frees its memory instead of hiding it.
            dashboard.FormClosed += delegate {
                dashboard = null;
                // Posted so it runs after the form (and its cover images) is disposed.
                SynchronizationContext.Current.Post(_ => Native.TrimMemory(), null);
            };
        }
        dashboard.Show();
        if (dashboard.WindowState == FormWindowState.Minimized) dashboard.WindowState = FormWindowState.Normal;
        dashboard.Activate();
    }

    public void SetPaused(bool value) {
        paused = value;
        config.StartPaused = value;
        if (!value && Running().Count > 0 && !active) {
            existingSession = true;
            attemptedSession = true;
        }
        SaveConfiguration();
        Store.Log(value ? "Pausado." : "Retomado.");
    }

    public void RefreshCatalog() {
        forceCatalog = true;
        nextCatalog = DateTime.MinValue;
    }

    public void SetSkipCompetitive(bool enabled) {
        RequireNoGames(); // changing the list mid-match could switch the resolution during play
        config.SkipCompetitive = enabled;
        SaveConfiguration();
        Store.Log(enabled ? "Jogos competitivos ficam na resolução normal." : "Jogos competitivos também usam a resolução de jogo.");
    }

    public void SetTaskbarRecovery(bool enabled) {
        config.AutoRefreshTaskbar = enabled;
        if (!enabled) ShellRefresh.Cancel();
        SaveConfiguration();
    }

    public void SetStartup(bool enabled) { StartupShortcut.Set(enabled); }

    public void RequestTaskbarRefresh() {
        ShellRefresh.Request();
        nextShellCheck = DateTime.MinValue;
    }

    public void OpenLog() {
        if (!File.Exists(Store.LogFile)) Store.Log("Registro iniciado.");
        // Full system path, so a notepad.exe elsewhere on PATH or in the working folder is never picked up.
        Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"), "\"" + Store.LogFile + "\"");
    }

    public void Quit() {
        if (Running().Count > 0) throw new Exception("Feche o jogo antes de sair.");
        if (!Restore()) throw new Exception("Não deu para voltar à resolução normal. Veja o Histórico nas Configurações.");
        ExitThread();
    }

    void RequireNoGames() {
        if (active || Running().Count > 0) throw new Exception("Feche o jogo antes de mudar isso.");
    }

    public void ChangeDisplay(string device) {
        RequireNoGames();
        config.Display = device;
        FillDisplayDefaults(true);
        SaveConfiguration();
    }

    public void ChangeResolution(int width, int height) {
        RequireNoGames();
        // Ask the driver to validate the mode (no change is made) before saving it.
        var mode = Native.Find(Device(), width, height, Native.Current(Device()).hz);
        int test = Native.Set(Device(), mode, true);
        if (test != 0) throw new Exception("O driver recusou " + width + " × " + height + ". Veja se ela está ativa no painel da placa de vídeo.");
        config.Width = width;
        config.Height = height;
        SaveConfiguration();
    }

    static bool ExecutableRunning(string exe) {
        return Native.ProcessIds().Any(pid => String.Equals(Native.ExePath(pid), exe, StringComparison.OrdinalIgnoreCase));
    }

    public void SetGameExcluded(string key, bool exclude) {
        var group = GamePreferences.Groups(config).FirstOrDefault(g => g.Key == key);
        if (group == null) return;
        if (group.Rules.Any(g => ExecutableRunning(g.Exe))) throw new Exception("Feche o jogo antes de mudar isso.");
        GamePreferences.Exclude(config, key, exclude);
        SaveConfiguration();
        Store.Log(group.Name + (exclude ? " agora fica na resolução normal." : " volta a usar a resolução de jogo."), LogKind.Library);
    }

    public bool IsKnownExe(string exe) { return config.Games.Any(g => String.Equals(g.Exe, exe, StringComparison.OrdinalIgnoreCase)); }

    public void AddGame(GameCandidate game) {
        bool running = game.Exes.Any(ExecutableRunning);
        GamePreferences.AddManual(config, game.Name, game.Exes);
        // A game that is already open switches on its next launch, never mid-session.
        if (running && !active) {
            existingSession = true;
            attemptedSession = true;
        }
        SaveConfiguration();
        Store.Log(game.Name + " adicionado.", LogKind.Library);
    }

    void SessionEnding(object sender, SessionEndingEventArgs e) { Restore(); }

    protected override void ExitThreadCore() {
        timer.Stop();
        DesktopLayout.StopTracking();
        if (!File.Exists(Store.State)) DesktopLayout.CancelDesktopRestore();
        foreach (var w in watchers) w.Dispose();
        showRequest.Dispose();
        if (dashboard != null) dashboard.Dispose();
        SystemEvents.SessionEnding -= SessionEnding;
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
