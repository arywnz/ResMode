using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Settings modal: one page with the master switch on top, then Tela, Jogos and Sistema.
public class SettingsOverlay : Modal {
    readonly AppContext owner;
    readonly Label title;
    readonly FlatButton closeButton, quitButton;
    readonly SmoothScroll general;
    readonly Panel generalColumn;
    readonly SelectBox monitors, resolutions, logLevel;
    readonly ToggleSwitch switching, competitive, startup, taskbar, autoUpdate, centerSecond;
    readonly Label resolutionHelp, updateInfo;
    readonly FlatButton updateButton, testButton;
    readonly System.Windows.Forms.Timer refresh;
    bool syncing;

    public SettingsOverlay(AppContext context, Func<Bitmap> windowSnapshot) : base(windowSnapshot) {
        owner = context;
        MaxCard = new Size(Theme.S(760), Theme.S(780));

        title = CardLabel("Configurações", Theme.Title(18), Theme.Text);
        title.AutoSize = true;
        closeButton = new FlatButton { Glyph = Theme.IconClose, Kind = ButtonKind.Quiet, Width = Theme.S(36) };
        closeButton.Click += delegate { Close(); };

        general = new SmoothScroll { BackColor = Theme.Surface };
        generalColumn = general.Content;
        generalColumn.BackColor = Theme.Surface;

        switching = new ToggleSwitch { BackColor = Theme.Surface };
        switching.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetPaused(!switching.Checked)); };
        monitors = new SelectBox { Width = Theme.S(210) };
        monitors.SelectionCommitted += delegate { Act(() => { owner.ChangeDisplay(((DisplayChoice)monitors.SelectedItem).Device); ReloadResolutions(); }); };
        resolutions = new SelectBox { Width = Theme.S(210), Technical = true, Font = Theme.Mono(10.5f) };
        resolutions.SelectionCommitted += delegate {
            var r = (ResolutionChoice)resolutions.SelectedItem;
            Act(() => owner.ChangeResolution(r.Width, r.Height));
        };
        var reload = new FlatButton { Glyph = Theme.IconRefresh, Kind = ButtonKind.Quiet, Width = Theme.S(36) };
        reload.Click += delegate { ReloadResolutions(); };
        new ToolTip().SetToolTip(reload, "Atualizar a lista");
        testButton = new FlatButton { Text = "Testar", Width = Theme.S(72) };
        new ToolTip().SetToolTip(testButton, "Liga a resolução escolhida por uns segundos e volta sozinha, para ver se o monitor aceita.");
        testButton.Click += delegate {
            var r = resolutions.SelectedItem as ResolutionChoice;
            if (r != null) Act(() => owner.TestResolution(r.Width, r.Height));
        };
        resolutionHelp = new Label { Cursor = Cursors.Hand };
        resolutionHelp.Click += delegate { ShowResolutionHelp(); };
        centerSecond = new ToggleSwitch();
        centerSecond.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetCenterSecondMonitor(centerSecond.Checked)); };
        competitive = new ToggleSwitch();
        competitive.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetSkipCompetitive(competitive.Checked)); };
        startup = new ToggleSwitch();
        startup.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetStartup(startup.Checked)); };
        taskbar = new ToggleSwitch();
        taskbar.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetTaskbarRecovery(taskbar.Checked)); };
        var fixNow = new FlatButton { Text = "Corrigir agora" };
        fixNow.Width = fixNow.PreferredWidth();
        fixNow.Click += delegate { Act(() => owner.RequestTaskbarRefresh()); };

        // What gets saved, and the button that turns it into a .txt the person can send.
        logLevel = new SelectBox { Width = Theme.S(170) };
        logLevel.Items = new List<object> { new LevelChoice("all", "Salvar tudo"), new LevelChoice("games", "Só os jogos"), new LevelChoice("off", "Não salvar") };
        logLevel.SelectionCommitted += delegate { Act(() => owner.SetLogLevel(((LevelChoice)logLevel.SelectedItem).Value)); };
        var tourButton = new FlatButton { Text = "Rever guia" };
        tourButton.Width = tourButton.PreferredWidth();
        tourButton.Click += delegate { owner.RequestTour(); Close(); }; // the window opens it once this card is gone
        var exportLog = new FlatButton { Text = "Gerar log", Glyph = Theme.IconFolder };
        exportLog.Width = exportLog.PreferredWidth();
        exportLog.Click += delegate { Act(() => owner.ExportLog()); };

        // One button does the whole job: look for an update, and once one is found, install it.
        updateInfo = new Label();
        new ToolTip().SetToolTip(updateInfo, "Mostra a versão instalada e o resultado da última busca.");
        updateButton = new FlatButton { Width = Theme.S(132), Kind = ButtonKind.Secondary };
        updateButton.Click += delegate { Act(() => { if (owner.Update != null) owner.InstallUpdate(); else owner.CheckForUpdates(true); }); };
        autoUpdate = new ToggleSwitch();
        new ToolTip().SetToolTip(autoUpdate, "Procurar sozinho ao abrir e a cada 15 minutos. Não envia nada seu.");
        autoUpdate.CheckedChanged += delegate { if (!syncing) Act(() => owner.SetCheckUpdates(autoUpdate.Checked)); };

        var blocks = new List<Control> {
            Row("Trocar resolução nos jogos", null, null, switching, true),
            Group("Tela",
                Row("Monitor", null, null, monitors),
                Row("Resolução nos jogos", null, resolutionHelp, Pair(Pair(testButton, reload), resolutions)),
                Row("Centralizar o 2º monitor", "Durante o jogo, alinha o segundo monitor ao meio do principal.", null, centerSecond)),
            Group("Jogos",
                Row("Ignorar competitivos", "Ficam na resolução nativa, onde FPS e latência pesam mais que nitidez.", null, competitive)),
            Group("Sistema",
                Row("Iniciar com o Windows", "Abre minimizado na bandeja.", null, startup),
                Row("Corrigir ícones borrados", "Refaz os ícones da barra de tarefas depois de jogar, sem reiniciar o Explorer.", null, Pair(fixNow, taskbar)),
                Row("Guia de boas-vindas", "Os quatro passos do primeiro uso.", null, tourButton),
                Row("Registro de atividade", "Erros ficam sempre salvos.", null, Pair(logLevel, exportLog)),
                Row("Procurar atualizações", null, updateInfo, Pair(updateButton, autoUpdate)))
        };
        foreach (var b in blocks) generalColumn.Controls.Add(b);
        foreach (var b in blocks) b.SizeChanged += delegate { Restack(); };
        general.Resize += delegate {
            generalColumn.Width = general.ClientSize.Width - Theme.S(16);
            foreach (Control b in generalColumn.Controls) b.Width = generalColumn.Width;
            Restack();
        };

        quitButton = new FlatButton { Text = "Sair do " + AppInfo.Name, Kind = ButtonKind.Quiet, Ink = Theme.Danger };
        quitButton.Width = quitButton.PreferredWidth();
        quitButton.Click += delegate { Act(() => owner.Quit()); };

        Controls.AddRange(new Control[] { title, closeButton, general, quitButton });

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
        int bodyTop = card.Y + Theme.S(76), footer = card.Bottom - Theme.S(20) - Theme.S(36);
        general.SetBounds(x, bodyTop, w + Theme.S(10), footer - Theme.S(14) - bodyTop);
        quitButton.Location = new Point(x - Theme.S(10), footer);
    }

    void Restack() {
        int y = 0;
        foreach (Control b in generalColumn.Controls) { b.Location = new Point(0, y); y += b.Height; }
        generalColumn.Height = y + Theme.S(12);
        general.Refit();
        // The card is as tall as its content (title, rows, footer), so a window that has the room shows no scrollbar;
        // when it does not (a small window), the card is capped at the window and the rows scroll.
        int wanted = Theme.S(76) + generalColumn.Height + Theme.S(14) + Theme.S(36) + Theme.S(20);
        if (MaxCard.Height != wanted) { MaxCard = new Size(MaxCard.Width, wanted); if (IsHandleCreated) PerformLayout(); }
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
            // Beside the rows, the name lines up with the first option's title (laid out first, so its position is current).
            rows[0].PerformLayout();
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
    // The hero row (the master switch) has a larger title and no description.
    Control Row(string name, string description, Label descriptionLabel, Control control, bool hero = false) {
        int baseHeight = hero ? Theme.S(72) : Theme.S(62), descTop = Theme.S(35);
        var row = new Panel { Height = baseHeight, BackColor = Theme.Surface };
        var label = new Label { Text = name, Font = hero ? Theme.Body(13, true) : Theme.Body(11, true), ForeColor = Theme.Text, AutoSize = true, BackColor = Theme.Surface, Location = new Point(0, Theme.S(11)) };
        var desc = descriptionLabel ?? new Label { Text = description };
        desc.Font = Theme.Body(10);
        desc.ForeColor = Theme.Muted;
        desc.BackColor = Theme.Surface;
        desc.AutoEllipsis = true;
        desc.Location = new Point(0, descTop);
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
                desc.Size = new Size(row.Width, Theme.S(22));
            } else {
                control.Location = new Point(row.Width - control.Width, (row.Height - control.Height) / 2);
                desc.Size = new Size(Math.Max(Theme.S(100), control.Left - Theme.S(20)), Theme.S(22));
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

    // The list shows only the resolution; the factor and refresh rate are in the explanation card.
    class ResolutionChoice {
        public int Width, Height;
        public override string ToString() { return Width + " × " + Height; }
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
            new InfoCard.Block { Heading = "No jogo", Body = "Nas opções de vídeo do jogo, escolha a mesma resolução desta lista. Em \"janela sem borda\", o jogo desenha uma janela do tamanho que você escolheu; se for menor que a tela, ele aparece como janela em vez de ocupar tudo." },
            new InfoCard.Block { Heading = "NVIDIA", Dot = Color.FromArgb(118, 185, 0),
                Body = "Painel de Controle NVIDIA › Gerenciar configurações 3D › DSR – Fatores. Os fatores DL (1,78× e 2,25×) usam um filtro de IA e chegam perto da nitidez do 4× com bem menos custo. Para uma resolução exata: Alterar resolução › Personalizar." },
            new InfoCard.Block { Heading = "AMD", Dot = Color.FromArgb(237, 28, 36),
                Body = "AMD Software: Adrenalin › Configurações › Tela › Super Resolução Virtual. Para uma resolução exata: Resoluções personalizadas, na mesma aba." });
        Theme.ShowNear(Theme.Popover(card), resolutionHelp, Theme.S(6));
    }

    // Reads the driver's modes (slow, so only when the modal opens or on request).
    void ReloadResolutions() {
        var cfg = owner.Configuration;
        var choices = new List<ResolutionChoice>();
        try {
            choices = Native.LargerModes(owner.DisplayDevice, cfg.DesktopWidth, cfg.DesktopHeight)
                .Select(m => new ResolutionChoice { Width = m.width, Height = m.height }).ToList();
        } catch { }
        var current = new ResolutionChoice { Width = cfg.Width, Height = cfg.Height };
        if (cfg.Width > 0 && !choices.Contains(current)) choices.Add(current);
        resolutions.Items = choices.Cast<object>().ToList();
        resolutions.SelectedItem = choices.FirstOrDefault(c => c.Equals(current));
        resolutions.Technical = choices.Count > 0;
        var previousFont = resolutions.Font; resolutions.Font = choices.Count > 0 ? Theme.Mono(10.5f) : Theme.Body(10.5f); previousFont.Dispose();
        if (choices.Count == 0) resolutions.Text = "Nenhuma disponível";
    }

    void Sync() {
        if (IsDisposed) return;
        var cfg = owner.Configuration;
        syncing = true;
        try {
            bool free = owner.RunningNames.Count == 0 && !owner.DisplayActive;
            switching.Checked = !owner.Paused;
            var screens = Screen.AllScreens.Select(s => new DisplayChoice { Device = s.DeviceName, Text = "Monitor " + new String(s.DeviceName.Where(Char.IsDigit).ToArray()) + (s.Primary ? " (principal)" : ""), Size = s.Bounds.Width + " × " + s.Bounds.Height }).ToList();
            monitors.Items = screens.Cast<object>().ToList();
            monitors.SelectedItem = screens.FirstOrDefault(s => s.Device == owner.DisplayDevice);
            monitors.Enabled = free;
            resolutions.Enabled = free && resolutions.Items.Count > 0;
            testButton.Enabled = free && resolutions.Items.Count > 0;
            resolutionHelp.Text = resolutions.Items.Count == 0 ? "Nenhuma maior que a tela. Como ativar" : "Como funciona na NVIDIA e na AMD";
            resolutionHelp.ForeColor = Theme.Text;
            if (!resolutionHelp.Font.Underline) resolutionHelp.Font = new Font(resolutionHelp.Font, FontStyle.Underline);
            centerSecond.Checked = cfg.CenterSecondMonitor;
            centerSecond.Enabled = free;
            competitive.Checked = cfg.SkipCompetitive;
            competitive.Enabled = free;
            startup.Checked = StartupShortcut.Enabled;
            taskbar.Checked = cfg.AutoRefreshTaskbar;
            autoUpdate.Checked = cfg.CheckUpdates;
            updateInfo.Text = !String.IsNullOrEmpty(owner.UpdateMessage) ? owner.UpdateMessage : AppInfo.Name + " " + AppInfo.Release;
            updateButton.Text = owner.UpdateBusy ? "Aguarde…" : owner.Update == null ? "Procurar agora" : owner.UpdateInPlace ? "Atualizar" : "Ver no GitHub";
            updateButton.Enabled = !owner.UpdateBusy;
            var level = logLevel.Items.Cast<LevelChoice>().FirstOrDefault(l => l.Value == (cfg.LogLevel ?? "all"));
            if (!Equals(logLevel.SelectedItem, level)) logLevel.SelectedItem = level;
        } finally { syncing = false; }
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
