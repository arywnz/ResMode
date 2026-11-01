using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Settings modal: "Geral" (switching, display, games, system) and "Histórico" (the log, filtered and coloured).
public class SettingsOverlay : Modal {
    readonly AppContext owner;
    readonly Label title;
    readonly FlatButton closeButton, quitButton, clearButton, openButton;
    readonly Segments tabs, logFilter;
    readonly SmoothScroll general;
    readonly Panel history, generalColumn;
    readonly SelectBox monitors, resolutions, logLevel;
    readonly ToggleSwitch switching, competitive, startup, taskbar;
    readonly Label resolutionHelp, switchingHelp;
    readonly LogView log;
    readonly System.Windows.Forms.Timer refresh;
    List<LogView.Entry> entries = new List<LogView.Entry>();
    string logStamp;
    bool syncing;

    public SettingsOverlay(AppContext context, Func<Bitmap> windowSnapshot) : base(windowSnapshot) {
        owner = context;
        MaxCard = new Size(Theme.S(760), Theme.S(700));

        title = CardLabel("Configurações", Theme.Title(18), Theme.Text);
        title.AutoSize = true;
        closeButton = new FlatButton { Glyph = Theme.IconClose, Kind = ButtonKind.Quiet, Width = Theme.S(36) };
        closeButton.Click += delegate { Close(); };
        tabs = new Segments { BackColor = Theme.Surface, Tabs = new List<Segments.Tab> { new Segments.Tab { Label = "Geral", Count = -1 }, new Segments.Tab { Label = "Histórico", Count = -1 } } };
        tabs.Width = tabs.PreferredWidth();
        tabs.Changed += delegate { ShowTab(); };

        // Geral ----------------------------------------------------------------
        general = new SmoothScroll { BackColor = Theme.Surface };
        generalColumn = general.Content;
        generalColumn.BackColor = Theme.Surface;

        switching = new ToggleSwitch { BackColor = Theme.Surface };
        switching.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetPaused(!switching.Checked)); };
        monitors = new SelectBox { Width = Theme.S(210) };
        monitors.SelectionCommitted += delegate { Act(() => { owner.ChangeDisplay(((DisplayChoice)monitors.SelectedItem).Device); ReloadResolutions(); }); };
        resolutions = new SelectBox { Width = Theme.S(210), Technical=true, Font=Theme.Mono(10.5f) };
        resolutions.SelectionCommitted += delegate {
            var r = (ResolutionChoice)resolutions.SelectedItem;
            Act(() => owner.ChangeResolution(r.Width, r.Height));
        };
        var reload = new FlatButton { Glyph = Theme.IconRefresh, Kind = ButtonKind.Quiet, Width = Theme.S(36) };
        reload.Click += delegate { ReloadResolutions(); };
        new ToolTip().SetToolTip(reload, "Atualizar a lista");
        resolutionHelp = new Label { Cursor = Cursors.Hand };
        resolutionHelp.Click += delegate { ShowResolutionHelp(); };
        switchingHelp = new Label();
        competitive = new ToggleSwitch();
        competitive.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetSkipCompetitive(competitive.Checked)); };
        startup = new ToggleSwitch();
        startup.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetStartup(startup.Checked)); };
        taskbar = new ToggleSwitch();
        taskbar.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetTaskbarRecovery(taskbar.Checked)); };
        var fixNow = new FlatButton { Text = "Corrigir agora", Kind = ButtonKind.Quiet, Font = Theme.Body(10) };
        fixNow.Width = fixNow.PreferredWidth();
        fixNow.Click += delegate { Act(() => owner.RequestTaskbarRefresh()); };

        var blocks = new List<Control> {
            Hero(),
            Group("Tela",
                Row("Monitor", null, null, monitors),
                Row("Resolução nos jogos", null, resolutionHelp, Pair(reload, resolutions))),
            Group("Jogos",
                Row("Ignorar competitivos", "Ficam na resolução nativa, onde FPS e latência pesam mais que nitidez.", null, competitive)),
            Group("Sistema",
                Row("Iniciar com o Windows", "Abre minimizado na bandeja.", null, startup),
                Row("Corrigir ícones borrados", "Reinicia a barra de tarefas depois de jogar.", null, Pair(fixNow, taskbar)))
        };
        foreach (var b in blocks) generalColumn.Controls.Add(b);
        foreach (var b in blocks) b.SizeChanged += delegate { Restack(); };
        general.Resize += delegate {
            generalColumn.Width = general.ClientSize.Width - Theme.S(16);
            foreach (Control b in generalColumn.Controls) b.Width = generalColumn.Width;
            Restack();
        };

        // Histórico ------------------------------------------------------------
        history = new Panel { BackColor = Theme.Surface, Visible = false };
        logFilter = new Segments { BackColor = Theme.Surface };
        logFilter.Changed += delegate { ShowLog(); };
        logLevel = new SelectBox { Width = Theme.S(200) };
        logLevel.Items = new List<object> { new LevelChoice("all", "Salvar tudo"), new LevelChoice("games", "Salvar só os jogos"), new LevelChoice("off", "Não salvar") };
        logLevel.SelectionCommitted += delegate { Act(() => owner.SetLogLevel(((LevelChoice)logLevel.SelectedItem).Value)); };
        new ToolTip().SetToolTip(logLevel, "Erros são sempre salvos.");
        log = new LogView { BackColor = Theme.Surface };
        history.Controls.AddRange(new Control[] { logFilter, logLevel, log });
        history.Layout += delegate {
            bool wrap = history.Width - logFilter.Width - Theme.S(24) < Theme.S(170);
            logFilter.Location = new Point(0, 0);
            logLevel.Location = wrap ? new Point(0, logFilter.Bottom + Theme.S(10)) : new Point(history.Width - logLevel.Width, 0);
            int top = (wrap ? logLevel.Bottom : logFilter.Bottom) + Theme.S(14);
            log.SetBounds(0, top, history.Width, history.Height - top);
        };

        // Footer -----------------------------------------------------------------
        quitButton = new FlatButton { Text = "Sair do " + AppInfo.Name, Kind = ButtonKind.Quiet, Ink = Theme.Danger };
        quitButton.Width = quitButton.PreferredWidth();
        quitButton.Click += delegate { Act(() => owner.Quit()); };
        clearButton = new FlatButton { Text = "Limpar histórico", Kind = ButtonKind.Quiet, Visible = false };
        clearButton.Width = clearButton.PreferredWidth();
        clearButton.Click += delegate {
            if (MessageBox.Show(FindForm(), "Apagar todo o histórico?", AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Act(() => owner.ClearLog());
            logStamp = null;
        };
        openButton = new FlatButton { Text = "Abrir arquivo", Visible = false };
        openButton.Width = openButton.PreferredWidth();
        openButton.Click += delegate { Act(() => owner.OpenLog()); };

        Controls.AddRange(new Control[] { title, closeButton, tabs, general, history, quitButton, clearButton, openButton });

        refresh = new System.Windows.Forms.Timer { Interval = 1000 };
        refresh.Tick += delegate { Sync(); };
        ReloadResolutions();
        Sync();
        refresh.Start();
    }

    protected override void LayoutCard(Rectangle card) {
        int pad = Theme.S(30), x = card.X + pad, right = card.Right - pad, w = right - x;
        title.Location = new Point(x - Theme.S(2), card.Y + Theme.S(24));
        closeButton.Location = new Point(card.Right - Theme.S(18) - closeButton.Width, card.Y + Theme.S(18));
        tabs.Location = new Point(x, card.Y + Theme.S(72));
        int bodyTop = tabs.Bottom + Theme.S(18), footer = card.Bottom - Theme.S(20) - Theme.S(36);
        general.SetBounds(x, bodyTop, w + Theme.S(10), footer - Theme.S(14) - bodyTop);
        history.SetBounds(x, bodyTop, w, footer - Theme.S(14) - bodyTop);
        quitButton.Location = new Point(x - Theme.S(10), footer);
        openButton.Location = new Point(right - openButton.Width, footer);
        clearButton.Location = new Point(openButton.Left - Theme.S(8) - clearButton.Width, footer);
    }


    // The base class reveals every control when the card finishes opening; keep only the current tab's.
    protected override void OnShown() { ShowTab(); }

    void ShowTab() {
        bool logs = tabs.Index == 1;
        general.Visible = !logs;
        history.Visible = clearButton.Visible = openButton.Visible = logs;
        if (logs) { logStamp = null; Sync(); }
    }

    void Restack() {
        int y = 0;
        foreach (Control b in generalColumn.Controls) { b.Location = new Point(0, y); y += b.Height; }
        generalColumn.Height = y + Theme.S(12);
        general.Refit();
    }

    // A section: its name in a narrow left column and its rows on the right, with a rule above.
    // The name does not take a row of its own, which is what lets the whole page fit without scrolling.
    // On narrow cards the name sits above the rows instead.
    Control Group(string name, params Control[] rows) {
        var group = new Panel { BackColor = Theme.Surface };
        var label = new Label { Text = name, Font = Theme.Body(10.5f, true), ForeColor = Theme.Muted, AutoSize = true, BackColor = Theme.Surface };
        group.Controls.Add(label);
        group.Controls.AddRange(rows);
        group.Paint += (s, e) => { using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, 0, 0, group.Width, 0); };
        group.Layout += delegate {
            bool side = group.Width >= Theme.S(560);
            int x = side ? Theme.S(128) : 0, y = side ? Theme.S(14) : Theme.S(46);
            foreach (var r in rows) {
                if (r.Left != x || r.Top != y || r.Width != group.Width - x) r.SetBounds(x, y, group.Width - x, r.Height);
                y += r.Height;
            }
            // Beside the rows, the name lines up with the first option's title.
            var first = rows[0].Controls[0];
            label.Location = side ? new Point(0, rows[0].Top + first.Top + (first.Height - label.Height) / 2) : new Point(0, Theme.S(18));
            int height = y + Theme.S(14);
            if (group.Height != height) group.Height = height;
        };
        foreach (var r in rows) r.SizeChanged += delegate { group.PerformLayout(); };
        return group;
    }

    Control Pair(Control a, Control b) {
        var p = new Panel { BackColor = Theme.Surface, Size = new Size(a.Width + Theme.S(8) + b.Width, Math.Max(a.Height, b.Height)) };
        a.Location = new Point(0, (p.Height - a.Height) / 2);
        b.Location = new Point(a.Width + Theme.S(8), (p.Height - b.Height) / 2);
        p.Controls.Add(a);
        p.Controls.Add(b);
        return p;
    }

    // Label and description on the left, control on the right; on narrow cards the control drops below.
    // The master switch: larger title, and a coloured dot that says at a glance whether it is on.
    Control Hero() {
        var row = (Panel)Row("Trocar resolução nos jogos", null, switchingHelp, switching, true);
        row.Paint += (s, e) => {
            Theme.Smooth(e.Graphics);
            int d = Theme.S(8), y = switchingHelp.Top + (switchingHelp.Height - d) / 2;
            using (var b = new SolidBrush(owner.Paused ? Theme.Warn : Theme.Live)) e.Graphics.FillEllipse(b, 0, y, d, d);
        };
        return row;
    }

    Control Row(string name, string description, Label descriptionLabel, Control control, bool hero = false) {
        int baseHeight = hero ? Theme.S(84) : Theme.S(62), descTop = hero ? Theme.S(48) : Theme.S(35), indent = hero ? Theme.S(16) : 0;
        var row = new Panel { Height = baseHeight, BackColor = Theme.Surface };
        var label = new Label { Text = name, Font = hero ? Theme.Body(13, true) : Theme.Body(11, true), ForeColor = Theme.Text, AutoSize = true, BackColor = Theme.Surface, Location = new Point(0, hero ? Theme.S(16) : Theme.S(11)) };
        var desc = descriptionLabel ?? new Label { Text = description };
        desc.Font = Theme.Body(10);
        desc.ForeColor = Theme.Muted;
        desc.BackColor = Theme.Surface;
        desc.AutoEllipsis = true;
        desc.Location = new Point(indent, descTop);
        bool described = descriptionLabel != null || description != null;
        row.Controls.Add(label);
        if (described) row.Controls.Add(desc);
        row.Controls.Add(control);
        row.Layout += delegate {
            bool narrow = row.Width < Theme.S(480);
            if (!described) label.Top = narrow ? Theme.S(11) : (row.Height - label.Height) / 2;
            int height = narrow ? baseHeight + control.Height + Theme.S(10) : baseHeight;
            if (row.Height != height) { row.Height = height; return; }
            if (narrow) {
                control.Location = new Point(0, baseHeight);
                desc.Size = new Size(row.Width - indent, Theme.S(22));
            } else {
                control.Location = new Point(row.Width - control.Width, (row.Height - control.Height) / 2);
                desc.Size = new Size(Math.Max(Theme.S(100), control.Left - Theme.S(20) - indent), Theme.S(22));
            }
            row.Invalidate();
        };
        return row;
    }

    class DisplayChoice : IListItem {
        public string Device, Text, Size;
        public string Label { get { return Text; } }
        public string Detail { get { return Size; } }
        public override string ToString() { return Text; }
        public override bool Equals(object o) { var d = o as DisplayChoice; return d != null && d.Device == Device; }
        public override int GetHashCode() { return Device.GetHashCode(); }
    }

    class ResolutionChoice : IListItem {
        public int Width, Height, MaxHz;
        public double Factor;
        public string Label { get { return Width + " × " + Height; } }
        // Factor over the desktop's pixels, then the best refresh rate the driver offers at that size.
        public string Detail {
            get {
                string factor = Factor.ToString("0.##") + "×";
                return factor + (MaxHz > 0 ? "    " + MaxHz + " Hz" : "");
            }
        }
        public override string ToString() {
            string tag = Width == 3840 && Height == 2160 ? "4K" : Width == 2560 && Height == 1440 ? "QHD" : Width == 5120 && Height == 2880 ? "5K" : Factor.ToString("0.##") + "×";
            return Width + " × " + Height + "  (" + tag + ")";
        }
        public override bool Equals(object o) { var r = o as ResolutionChoice; return r != null && r.Width == Width && r.Height == Height; }
        public override int GetHashCode() { return Width * 10000 + Height; }
    }

    class LevelChoice {
        public string Value, Text;
        public LevelChoice(string value, string text) { Value = value; Text = text; }
        public override string ToString() { return Text; }
        public override bool Equals(object o) { var l = o as LevelChoice; return l != null && l.Value == Value; }
        public override int GetHashCode() { return Value.GetHashCode(); }
    }

    void ShowResolutionHelp() {
        var card = new InfoCard("Como funciona", Theme.S(400),
            new InfoCard.Block { Body = "O jogo é renderizado numa resolução maior que a do monitor e a placa de vídeo reduz a imagem para a tela. Fica mais nítido e com menos serrilhado, mas pesa como rodar naquela resolução de verdade." },
            new InfoCard.Block { Body = "A lista mostra só o que o driver oferece acima da resolução da tela. Depois de ativar algo novo, use o botão de atualizar ao lado da lista." },
            new InfoCard.Block { Heading = "NVIDIA", Dot = Color.FromArgb(118, 185, 0),
                Body = "Painel de Controle NVIDIA › Gerenciar configurações 3D › DSR – Fatores. Os fatores DL (1,78× e 2,25×) usam um filtro de IA e chegam perto da nitidez do 4× com bem menos custo. Para uma resolução exata: Alterar resolução › Personalizar." },
            new InfoCard.Block { Heading = "AMD", Dot = Color.FromArgb(237, 28, 36),
                Body = "AMD Software: Adrenalin › Configurações › Tela › Super Resolução Virtual. Para uma resolução exata: Resoluções personalizadas, na mesma aba." });
        Theme.ShowNear(Theme.Popover(card), resolutionHelp, Theme.S(6));
    }

    // Reads the driver's modes (slow, so only when the modal opens or on request).
    void ReloadResolutions() {
        var cfg = owner.Configuration;
        double desktop = Math.Max(1, cfg.DesktopWidth * (double)cfg.DesktopHeight);
        var choices = new List<ResolutionChoice>();
        try {
            choices = Native.LargerModes(owner.DisplayDevice, cfg.DesktopWidth, cfg.DesktopHeight)
                .Select(m => new ResolutionChoice { Width = m.width, Height = m.height, MaxHz = m.hz, Factor = m.width * (double)m.height / desktop }).ToList();
        } catch { }
        var current = new ResolutionChoice { Width = cfg.Width, Height = cfg.Height, Factor = cfg.Width * (double)cfg.Height / desktop };
        if (cfg.Width > 0 && !choices.Contains(current)) choices.Add(current);
        resolutions.Items = choices.Cast<object>().ToList();
        resolutions.SelectedItem = choices.FirstOrDefault(c => c.Equals(current));
        resolutions.Technical=choices.Count>0;
        var previousFont=resolutions.Font;resolutions.Font=choices.Count>0?Theme.Mono(10.5f):Theme.Body(10.5f);previousFont.Dispose();
        if (choices.Count == 0) resolutions.Text = "Nenhuma disponível";
    }

    void Sync() {
        if (IsDisposed) return;
        var cfg = owner.Configuration;
        syncing = true;
        try {
            bool free = owner.RunningNames.Count == 0 && !owner.DisplayActive;
            switching.Checked = !owner.Paused;
            switchingHelp.Text = owner.Paused ? "Desligado. A tela fica como está." : "Muda ao abrir um jogo e volta ao fechar.";
            if (switchingHelp.Parent != null) switchingHelp.Parent.Invalidate();
            var screens = Screen.AllScreens.Select(s => new DisplayChoice { Device = s.DeviceName, Text = "Monitor " + new String(s.DeviceName.Where(Char.IsDigit).ToArray()) + (s.Primary ? " (principal)" : ""), Size = s.Bounds.Width + " × " + s.Bounds.Height }).ToList();
            monitors.Items = screens.Cast<object>().ToList();
            monitors.SelectedItem = screens.FirstOrDefault(s => s.Device == owner.DisplayDevice);
            monitors.Enabled = free;
            resolutions.Enabled = free && resolutions.Items.Count > 0;
            var sel = resolutions.SelectedItem as ResolutionChoice;
            resolutionHelp.Text = resolutions.Items.Count == 0 ? "Nenhuma maior que a tela. Como ativar" : "Como funciona na NVIDIA e na AMD";
            resolutionHelp.ForeColor = Theme.Text;
            if (!resolutionHelp.Font.Underline) resolutionHelp.Font = new Font(resolutionHelp.Font, FontStyle.Underline);
            competitive.Checked = cfg.SkipCompetitive;
            competitive.Enabled = free;
            startup.Checked = StartupShortcut.Enabled;
            taskbar.Checked = cfg.AutoRefreshTaskbar;
            var level = logLevel.Items.Cast<LevelChoice>().FirstOrDefault(l => l.Value == (cfg.LogLevel ?? "all"));
            if (!Equals(logLevel.SelectedItem, level)) logLevel.SelectedItem = level;
        } finally { syncing = false; }

        if (history.Visible) {
            var fi = new FileInfo(Store.LogFile);
            string stamp = fi.Exists ? fi.LastWriteTimeUtc.Ticks.ToString() : "";
            if (stamp != logStamp) { logStamp = stamp; ReadLog(); }
        }
    }

    void ReadLog() {
        entries = new List<LogView.Entry>();
        try {
            var files = Directory.GetFiles(Store.Root, AppInfo.LogName + "*").OrderByDescending(f => f == Store.LogFile ? "~" : f);
            foreach (string f in files) {
                foreach (string line in Store.ReadLogLines(f).Reverse()) {
                    var e = LogView.Parse(line);
                    if (e != null) entries.Add(e);
                    if (entries.Count >= 1500) break;
                }
                if (entries.Count >= 1500) break;
            }
        } catch { }
        ShowLog();
    }

    void ShowLog() {
        var kinds = new LogKind?[] { null, LogKind.Game, LogKind.Library, LogKind.System, LogKind.Error };
        var labels = new[] { "Tudo", "Jogos", "Biblioteca", "Sistema", "Erros" };
        int index = logFilter.Index;
        logFilter.Tabs = kinds.Select((k, i) => new Segments.Tab { Label = labels[i], Count = k == null ? entries.Count : entries.Count(e => e.Kind == k) }).ToList();
        logFilter.Width = logFilter.PreferredWidth();
        logFilter.Select(index, false);
        var kind = kinds[logFilter.Index];
        log.EmptyText = kind == LogKind.Error ? "Nenhum erro registrado." : "Histórico vazio.";
        log.SetEntries(entries.Where(e => kind == null || e.Kind == kind));
        history.PerformLayout();
    }

    void Act(Action action) {
        try {
            action();
            Sync();
        } catch (Exception e) {
            MessageBox.Show(FindForm(), e.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Sync();
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) refresh.Dispose();
        base.Dispose(disposing);
    }
}
