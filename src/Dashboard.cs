using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// The whole app in one screen: a top bar (search, refresh, add, notifications, settings),
// launcher tabs and the cover grid. Settings and "Adicionar jogo" open as modals.
public class Dashboard : Form {
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);

    readonly AppContext owner;
    readonly Panel topBar, tabsRow;
    readonly PictureBox logo;
    readonly Label brand;
    readonly SearchBox search;
    readonly RefreshButton refreshButton;
    readonly FlatButton addButton, settingsButton;
    readonly BellButton bell;
    readonly Segments filter;
    readonly GameGrid grid;
    readonly System.Windows.Forms.Timer refresh;
    Modal modal;
    int version = -1;
    string runningKey = "";
    bool closing;

    public Dashboard(AppContext context) {
        owner = context;
        Text = AppInfo.Name;
        Icon = new Icon(Store.IconFile);
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Base;
        ForeColor = Theme.Text;
        Font = Theme.Body(10);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(Theme.S(1120), Theme.S(760));
        MinimumSize = new Size(Theme.S(560), Theme.S(480));
        Opacity = 0;

        // Top bar ------------------------------------------------------------------
        topBar = new Panel { Dock = DockStyle.Top, Height = Theme.S(76) };
        logo = new PictureBox { Image = Theme.IconImage(Store.IconFile, Theme.S(30)), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(Theme.S(30), Theme.S(30)) };
        brand = new Label { Text = AppInfo.Name, AutoSize = true, Font = Theme.Title(16), ForeColor = Theme.Text };
        search = new SearchBox();
        search.Input.TextChanged += delegate { ReloadGames(false); };
        refreshButton = new RefreshButton();
        new ToolTip().SetToolTip(refreshButton, "Atualizar biblioteca");
        refreshButton.Click += delegate { RefreshLibrary(); };
        addButton = new FlatButton { Text = "Adicionar jogo", Kind = ButtonKind.Primary };
        addButton.Width = addButton.PreferredWidth();
        addButton.Click += delegate { ShowModal(new AddGameOverlay(owner, WindowSnapshot)); };
        bell = new BellButton();
        new ToolTip().SetToolTip(bell, "Notificações");
        bell.Click += delegate { ShowNotices(); };
        settingsButton = new FlatButton { Glyph = Theme.IconSettings, Kind = ButtonKind.Quiet, Width = Theme.S(36) };
        new ToolTip().SetToolTip(settingsButton, "Configurações");
        settingsButton.Click += delegate { ShowModal(new SettingsOverlay(owner, WindowSnapshot)); };
        topBar.Controls.AddRange(new Control[] { logo, brand, search, refreshButton, addButton, bell, settingsButton });
        topBar.Layout += delegate { LayoutTopBar(); };
        topBar.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1); };

        // Launcher tabs and grid -----------------------------------------------------
        tabsRow = new Panel { Dock = DockStyle.Top, Height = Theme.S(70) };
        filter = new Segments();
        filter.Changed += delegate { ReloadGames(true); };
        tabsRow.Controls.Add(filter);
        tabsRow.Layout += delegate { filter.Location = new Point(Theme.S(28), Theme.S(20)); };
        var gridHolder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.S(16), 0, Theme.S(18), Theme.S(12)) };
        grid = new GameGrid { Dock = DockStyle.Fill, IsRunning = g => owner.RunningNames.Contains(g.Name) };
        grid.MenuRequested += GameMenu;
        grid.PlayRequested += Play;
        gridHolder.Controls.Add(grid);

        Controls.Add(gridHolder);
        Controls.Add(tabsRow);
        Controls.Add(topBar);

        refresh = new System.Windows.Forms.Timer { Interval = 1000 };
        refresh.Tick += delegate { UpdateView(); };
        Shown += delegate {
            UpdateView();
            refresh.Start();
            Motion.Run(Tuple.Create(this, "fade"), Motion.Normal, e => Opacity = e);
        };
    }

    // Right-to-left: settings, bell, add, refresh, search. The name hides and the search shrinks on narrow windows.
    void LayoutTopBar() {
        int pad = Theme.S(28), gap = Theme.S(8), cy = topBar.Height / 2;
        logo.Location = new Point(pad, cy - logo.Height / 2);
        brand.Location = new Point(logo.Right + Theme.S(12), cy - brand.Height / 2);
        int x = topBar.Width - pad;
        foreach (var c in new Control[] { settingsButton, bell, addButton, refreshButton }) {
            x -= c.Width;
            c.Location = new Point(x, cy - c.Height / 2);
            x -= gap;
        }
        int left = brand.Right + Theme.S(24);
        brand.Visible = x - left >= Theme.S(180);
        if (!brand.Visible) left = logo.Right + Theme.S(16);
        search.Width = Math.Max(Theme.S(120), Math.Min(Theme.S(280), x - left - gap));
        search.Location = new Point(x - search.Width, cy - search.Height / 2);
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        try {
            int dark = 1, round = 2, caption = ColorTranslator.ToWin32(Theme.Base), text = ColorTranslator.ToWin32(Theme.Text);
            DwmSetWindowAttribute(Handle, 20, ref dark, 4);     // dark title bar
            DwmSetWindowAttribute(Handle, 33, ref round, 4);    // rounded window corners
            DwmSetWindowAttribute(Handle, 35, ref caption, 4);  // title bar color matches the window
            DwmSetWindowAttribute(Handle, 36, ref text, 4);
        } catch { }
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized) Native.TrimMemory();
    }

    // Closing fades the window out first.
    protected override void OnFormClosing(FormClosingEventArgs e) {
        if (!closing && e.CloseReason == CloseReason.UserClosing) {
            e.Cancel = true;
            closing = true;
            Motion.Run(Tuple.Create(this, "fade"), Motion.Fast, v => Opacity = 1 - v, Close);
            return;
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        if (keyData == Keys.Escape && modal != null) { modal.Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // Modals ----------------------------------------------------------------------

    // Snapshot of the window behind a modal, so it can be dimmed underneath.
    Bitmap WindowSnapshot() {
        var b = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        foreach (Control c in Controls)
            if (c.Visible && c != modal) c.DrawToBitmap(b, c.Bounds);
        return b;
    }

    void ShowModal(Modal m) {
        if (modal != null) { m.Dispose(); return; }
        modal = m;
        // Positioned (not docked) so it covers the whole window.
        modal.Bounds = new Rectangle(Point.Empty, ClientSize);
        modal.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        modal.Closed += delegate {
            Controls.Remove(m);
            m.Dispose();
            modal = null;
            UpdateView();
        };
        Controls.Add(modal);
        modal.BringToFront();
        modal.Open();
    }

    void ShowNotices() {
        var list = new NoticeList(owner);
        int margin = Theme.S(12), gap = Theme.S(8);
        var work = Screen.FromControl(bell).WorkingArea;
        var anchor = bell.PointToScreen(new Point(bell.Width, bell.Height + gap));
        int width = Math.Min(Theme.S(460), Math.Min(ClientSize.Width - margin * 2, work.Width - margin * 2));
        int below = work.Bottom - margin - anchor.Y;
        int above = bell.PointToScreen(Point.Empty).Y - work.Top - margin - gap;
        int height = Math.Min(Theme.S(540), Math.Min(ClientSize.Height - topBar.Height - margin, Math.Max(below, above) - 2));
        list.SetViewport(width, height);
        var drop = Theme.RoundedDrop();
        var host = new ToolStripControlHost(list) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = list.Size };
        list.SizeChanged += delegate { host.Size = list.Size; };
        drop.Items.Add(host);
        drop.Opening += delegate { drop.Opacity = 0; Motion.Run(Tuple.Create(drop, "fade"), Motion.Fast, e => { if (!drop.IsDisposed) drop.Opacity = e; }); };
        // Reopening after a resize measures the text and available screen space again.
        EventHandler closeOnMove = delegate { if (drop.Visible) drop.Close(); };
        SizeChanged += closeOnMove;
        LocationChanged += closeOnMove;
        drop.Closed += delegate {
            SizeChanged -= closeOnMove;
            LocationChanged -= closeOnMove;
            if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(drop.Dispose)); else drop.Dispose();
        };
        int popupHeight = list.Height + 2;
        int x = Math.Max(work.Left + margin, Math.Min(anchor.X - list.Width - 2, work.Right - margin - list.Width - 2));
        int y = below >= popupHeight ? anchor.Y : bell.PointToScreen(Point.Empty).Y - gap - popupHeight;
        y = Math.Max(work.Top + margin, Math.Min(y, work.Bottom - margin - popupHeight));
        drop.Show(new Point(x, y));
        list.FocusContent();
        owner.MarkNoticesRead();
        bell.Unread = 0;
        bell.Invalidate();
    }

    void RefreshLibrary() {
        if (owner.RunningNames.Count > 0) {
            owner.Notify("Biblioteca atualiza depois do jogo", "Com um jogo aberto, a busca espera ele fechar para não pesar.", LogKind.Library);
            UpdateView();
            return;
        }
        owner.RefreshCatalog();
        refreshButton.Start(() => owner.CatalogPending);
    }

    // Library ---------------------------------------------------------------------

    // Tabs: all games in use, one per launcher that has games, then ignored ones.
    void ReloadGames(bool animate) {
        var all = GamePreferences.Groups(owner.Configuration);
        var active = all.Where(g => !g.Excluded).ToList();
        object selectedTag = filter.Tabs.Count > filter.Index ? filter.Tabs[filter.Index].Tag : "all";
        var tabs = new List<Segments.Tab> { new Segments.Tab { Label = "Todos", Count = active.Count, Tag = "all" } };
        foreach (var l in Launchers.All) {
            int count = active.Count(g => g.Launcher == l);
            if (count > 0) tabs.Add(new Segments.Tab { Label = l == Launchers.Manual ? "Outros" : l.Name, Count = count, Icon = l.Icon, Tag = l });
        }
        int removed = all.Count - active.Count;
        if (removed > 0) tabs.Add(new Segments.Tab { Label = "Ignorados", Count = removed, Tag = "removed" });
        filter.Tabs = tabs;
        filter.Width = filter.PreferredWidth();
        int index = tabs.FindIndex(t => Equals(t.Tag, selectedTag));
        filter.Select(index < 0 ? 0 : index, false);
        var tag = tabs[filter.Index].Tag;

        string q = search.Input.Text.Trim();
        var shown = (Equals(tag, "removed") ? all.Where(g => g.Excluded) : active.Where(g => Equals(tag, "all") || g.Launcher == tag))
            .Where(g => q == "" || g.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0)
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        grid.EmptyText = q != "" ? "Nenhum jogo com \"" + q + "\"." : "Nenhum jogo encontrado.";
        grid.SetGames(shown, animate);
        Artwork.Load(all, GameGrid.Cover, () => { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(grid.Invalidate)); });
    }

    // Popover beside the clicked card (never on top of it, so a second click on the card still starts the game).
    void GameMenu(LibraryGame g, Rectangle card) {
        var panel = new GamePanel(g, owner.RunningNames.Contains(g.Name));
        var drop = Theme.RoundedDrop();
        drop.Items.Add(new ToolStripControlHost(panel) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = panel.Size });
        drop.Opening += delegate { drop.Opacity = 0; Motion.Run(Tuple.Create(drop, "fade"), Motion.Fast, e => { if (!drop.IsDisposed) drop.Opacity = e; }); };
        drop.Closed += delegate { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(drop.Dispose)); else drop.Dispose(); };

        panel.Play.Click += delegate { drop.Close(); Play(g, new Point(card.Right, card.Top + Theme.S(24))); };
        panel.Toggle.Click += delegate { drop.Close(); Act(() => owner.SetGameExcluded(g.Key, !g.Excluded)); };
        string exe = g.Rules.Select(r => r.Exe).FirstOrDefault(File.Exists);
        panel.Folder.Enabled = exe != null;
        panel.Folder.Click += delegate {
            drop.Close();
            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Act(() => Process.Start(explorer, "/select,\"" + exe + "\""));
        };

        var screen = grid.RectangleToScreen(card);
        var work = Screen.FromControl(grid).WorkingArea;
        var window = RectangleToScreen(ClientRectangle);
        int gap = Theme.S(10), w = panel.Width + 2, h = panel.Height + 2;
        int x = screen.Right + gap;
        if (x + w > Math.Min(window.Right, work.Right)) x = screen.Left - gap - w;
        x = Math.Max(work.Left, Math.Min(x, work.Right - w));
        int y = Math.Max(work.Top + gap, Math.Min(screen.Top, work.Bottom - gap - h));
        drop.Show(new Point(x, y));
        panel.Play.Focus();
    }

    // Steam starts its own games; otherwise the game's exe, asking which one when it has several.
    void Play(LibraryGame g, Point at) {
        if (owner.RunningNames.Contains(g.Name)) return;
        var exes = GameLaunch.Executables(g);
        if (g.Launcher == Launchers.Steam || exes.Count <= 1) { Act(() => GameLaunch.Start(g)); return; }
        var menu = Theme.Menu();
        menu.Items.Add(new ToolStripMenuItem("Abrir com") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        foreach (string path in exes) {
            string selected = path;
            menu.Items.Add(new ToolStripMenuItem(Path.GetFileName(path), null, delegate { Act(() => GameLaunch.Start(g, selected)); }) { ToolTipText = path });
        }
        Theme.ShowMenu(menu, grid, at);
    }

    void UpdateView() {
        if (IsDisposed) return;
        string running = String.Join("|", owner.RunningNames);
        if (version != owner.LibraryVersion) { version = owner.LibraryVersion; ReloadGames(false); }
        else if (running != runningKey) grid.Invalidate();
        runningKey = running;
        // Background scans (a game just installed, for example) also spin the refresh icon.
        if (owner.CatalogBusy && !refreshButton.Spinning) refreshButton.Start(() => owner.CatalogPending);
        if (bell.Unread != owner.Unread) { bell.Unread = owner.Unread; bell.Invalidate(); }
    }

    void Act(Action action) {
        try {
            action();
            UpdateView();
        } catch (Exception e) {
            MessageBox.Show(this, e.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (refresh != null) refresh.Dispose();
            Artwork.Clear();
        }
        base.Dispose(disposing);
    }
}
