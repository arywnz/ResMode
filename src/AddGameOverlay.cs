using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// "Adicionar jogo" modal.
// Adicionar: uses the pasted path, or opens the file picker when the box is empty.
// Procurar no PC: deep search of the fixed drives, only when clicked.
public class AddGameOverlay : Modal {
    readonly AppContext owner;
    CancellationTokenSource search;

    readonly Label title, intro, error, heading, status;
    readonly SearchBox pathBox;
    readonly FlatButton addButton, searchButton, closeButton;
    readonly CandidateList list;

    public AddGameOverlay(AppContext context, Func<Bitmap> windowSnapshot) : base(windowSnapshot) {
        owner = context;
        AllowDrop = true;

        title = CardLabel("Adicionar jogo", Theme.Title(18), Theme.Text);
        title.AutoSize = true;
        intro = CardLabel("Cole o caminho da pasta ou do .exe, ou procure no PC.", Theme.Body(10.5f), Theme.Muted);
        pathBox = new SearchBox { Placeholder = @"D:\Jogos\Nome do jogo", Glyph = Theme.IconFolder, BackColor = Theme.Surface };
        pathBox.Input.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Add(); } };
        addButton = new FlatButton { Text = "Adicionar", Kind = ButtonKind.Primary };
        addButton.Width = addButton.PreferredWidth();
        addButton.Click += delegate { Add(); };
        searchButton = new FlatButton { Text = "Procurar no PC", Glyph = Theme.IconSearch };
        searchButton.Width = searchButton.PreferredWidth();
        searchButton.Click += delegate { ToggleSearch(); };
        closeButton = new FlatButton { Glyph = Theme.IconClose, Kind = ButtonKind.Quiet, Width = Theme.S(36) };
        closeButton.Click += delegate { Close(); };
        error = CardLabel("", Theme.Body(10), Theme.Warn);
        heading = CardLabel("Encontrados no PC", Theme.Body(11.5f, true), Theme.Text);
        heading.AutoSize = true;
        status = CardLabel("", Theme.Body(10), Theme.Muted);
        status.AutoEllipsis = true;
        list = new CandidateList { BackColor = Theme.Surface, EmptyText = "A busca olha todos os discos, inclusive jogos fora de launcher." };
        list.AddRequested += AddCandidate;

        Controls.AddRange(new Control[] { title, intro, pathBox, addButton, searchButton, closeButton, error, heading, status, list });

        DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (s, e) => {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) { pathBox.Input.Text = files[0]; Add(); }
        };
    }

    protected override void OnShown() { pathBox.Input.Focus(); }
    protected override void OnClosing() { if (search != null) search.Cancel(); }

    protected override void LayoutCard(Rectangle card) {
        int pad = Theme.S(30), x = card.X + pad, right = card.Right - pad, w = right - x;
        title.Location = new Point(x - Theme.S(2), card.Y + Theme.S(24));
        closeButton.Location = new Point(card.Right - Theme.S(18) - closeButton.Width, card.Y + Theme.S(18));
        intro.SetBounds(x, card.Y + Theme.S(64), w, Theme.S(24));
        searchButton.Location = new Point(right - searchButton.Width, card.Y + Theme.S(102));
        addButton.Location = new Point(searchButton.Left - Theme.S(8) - addButton.Width, searchButton.Top);
        pathBox.SetBounds(x, searchButton.Top, addButton.Left - Theme.S(8) - x, Theme.S(36));
        error.SetBounds(x, card.Y + Theme.S(144), w, Theme.S(24));
        heading.Location = new Point(x, card.Y + Theme.S(182));
        status.SetBounds(x, card.Y + Theme.S(208), w, Theme.S(24));
        list.SetBounds(x, card.Y + Theme.S(240), w + Theme.S(10), card.Bottom - Theme.S(22) - (card.Y + Theme.S(240)));
    }

    void Ui(Action a) { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }

    void ShowError(string text, bool warning = true) {
        error.ForeColor = warning ? Theme.Warn : Theme.Muted;
        error.Text = text;
    }

    void Add() {
        string text = pathBox.Input.Text.Trim();
        if (text == "") {
            using (var f = new OpenFileDialog { Title = "Escolha o executável do jogo", Filter = "Executável (*.exe)|*.exe" }) {
                if (f.ShowDialog(FindForm()) != DialogResult.OK) return;
                text = f.FileName;
            }
        }
        ShowError("");
        try {
            var found = GameFinder.FromPath(text, owner.IsKnownExe);
            if (found.Count == 1) {
                owner.AddGame(found[0]);
                list.Add(found[0], true, true);
                pathBox.Input.Text = "";
                ShowError(found[0].Name + " adicionado.", false);
            } else {
                foreach (var g in found.AsEnumerable().Reverse()) list.Add(g, true, false);
                ShowError(found.Count + " jogos nessa pasta. Escolha quais adicionar na lista.", false);
            }
        } catch (Exception ex) {
            ShowError(ex.Message);
        }
    }

    void AddCandidate(GameCandidate game) {
        try {
            owner.AddGame(game);
            list.MarkAdded(game);
        } catch (Exception ex) { ShowError(ex.Message); }
    }

    void ToggleSearch() {
        if (search != null) { search.Cancel(); return; }
        search = new CancellationTokenSource();
        var token = search.Token;
        searchButton.Text = "Parar";
        searchButton.Width = searchButton.PreferredWidth();
        PerformLayout();
        status.Text = "Procurando…";
        list.EmptyText = "";
        list.Invalidate();
        var lastProgress = DateTime.MinValue;
        int before = list.Count;
        Task.Factory.StartNew(() => {
            string result;
            try {
                GameFinder.Search(owner.IsKnownExe, game => Ui(() => list.Add(game)), folder => {
                    if ((DateTime.Now - lastProgress).TotalMilliseconds < 200) return;
                    lastProgress = DateTime.Now;
                    Ui(() => status.Text = "Procurando em " + folder);
                }, token);
                result = null;
            } catch (OperationCanceledException) {
                result = "Busca interrompida.";
            } catch (Exception ex) {
                result = "Erro na busca: " + ex.Message;
            }
            Ui(() => {
                int found = list.Count - before;
                status.Text = result ?? (found == 0 ? "Nenhum jogo novo encontrado." : found + (found == 1 ? " jogo encontrado." : " jogos encontrados."));
                if (list.Count == 0) list.EmptyText = "Nenhum jogo novo. Se faltou algum, cole o caminho dele acima.";
                list.Invalidate();
                search.Dispose();
                search = null;
                searchButton.Text = "Procurar de novo";
                searchButton.Width = searchButton.PreferredWidth();
                PerformLayout();
            });
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (search != null) search.Cancel();
            list.Clear();
        }
        base.Dispose(disposing);
    }
}

// Found games, one row each: icon, name, folder, and an Add button that turns into "Adicionado".
public class CandidateList : ScrollSurface {
    class Row { public GameCandidate Game; public Image Icon; public bool Added; public float Appear; }
    readonly List<Row> rows = new List<Row>();
    public event Action<GameCandidate> AddRequested;
    public string EmptyText = "";
    int hot = -1;
    int RowH { get { return Theme.S(64); } }
    public int Count { get { return rows.Count; } }

    public CandidateList() { Font = Theme.Body(10); }

    public void Add(GameCandidate game, bool top = false, bool added = false) {
        var same = rows.FirstOrDefault(r => String.Equals(r.Game.Folder, game.Folder, StringComparison.OrdinalIgnoreCase) && r.Game.Exes.SequenceEqual(game.Exes));
        if (same != null) {
            if (added) { same.Added = true; Invalidate(); }
            return;
        }
        Image icon = null;
        try { icon = Launchers.ExtractIcon(game.Exes[0], Theme.S(32)); } catch { }
        var row = new Row { Game = game, Icon = icon, Added = added };
        if (top) rows.Insert(0, row); else rows.Add(row);
        content = rows.Count * RowH;
        Motion.Run(Tuple.Create(this, row), Motion.Normal, e => { row.Appear = e; Invalidate(); });
    }

    public void MarkAdded(GameCandidate game) {
        var row = rows.FirstOrDefault(r => r.Game == game);
        if (row != null) { row.Added = true; Invalidate(); }
    }

    public void Clear() {
        foreach (var r in rows) if (r.Icon != null) r.Icon.Dispose();
        rows.Clear();
    }

    Rectangle ButtonRect(int i) {
        int y = i * RowH - (int)offset;
        return new Rectangle(Width - Theme.S(138), y + (RowH - Theme.S(34)) / 2, Theme.S(112), Theme.S(34));
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        int h = -1;
        for (int i = 0; i < rows.Count; i++) if (!rows[i].Added && ButtonRect(i).Contains(e.Location)) h = i;
        if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e) {
        for (int i = 0; i < rows.Count; i++)
            if (!rows[i].Added && ButtonRect(i).Contains(e.Location) && AddRequested != null) AddRequested(rows[i].Game);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.Smooth(g);
        if (rows.Count == 0) {
            Theme.DrawText(g, EmptyText, Font, Theme.Muted, new Rectangle(0, 0, Width - Theme.S(16), Theme.S(60)), TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            return;
        }
        using (var bold = Theme.Body(10.5f, true)) using (var small = Theme.Body(9.5f))
            for (int i = 0; i < rows.Count; i++) {
                var row = rows[i];
                int y = i * RowH - (int)offset;
                if (y + RowH < 0 || y > Height) continue;
                int rise = (int)((1 - row.Appear) * Theme.S(8));
                var r = new Rectangle(0, y + Theme.S(4) + rise, Width - Theme.S(14), RowH - Theme.S(8));
                Theme.Fill(g, r, Theme.S(10), Theme.Raised);
                int s = Theme.S(32);
                if (row.Icon != null) g.DrawImage(row.Icon, r.X + Theme.S(14), r.Y + (r.Height - s) / 2, s, s);
                int tx = r.X + Theme.S(60), tw = r.Width - Theme.S(60) - Theme.S(146);
                Theme.DrawText(g, row.Game.Name, bold, Theme.Text, new Rectangle(tx, r.Y + Theme.S(7), tw, Theme.S(22)));
                Theme.DrawText(g, row.Game.Folder + (row.Game.Hint != null ? "   " + row.Game.Hint : ""), small, Theme.Muted, new Rectangle(tx, r.Y + Theme.S(29), tw, Theme.S(20)), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
                var b = ButtonRect(i);
                b.Y += rise;
                if (row.Added) {
                    using (var f = Theme.Glyphs(9)) Theme.DrawGlyph(g, Theme.IconCheck, f, Theme.Live, new Rectangle(b.X, b.Y, Theme.S(24), b.Height));
                    Theme.DrawText(g, "Adicionado", small, Theme.Live, new Rectangle(b.X + Theme.S(24), b.Y, b.Width, b.Height));
                } else {
                    Theme.Fill(g, b, Theme.S(8), i == hot ? Theme.Mix(Theme.Accent, Color.White, 0.18f) : Theme.Accent);
                    Theme.DrawText(g, "Adicionar", bold, Theme.AccentInk, b, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                if (row.Appear < 1) using (var veil = new SolidBrush(Color.FromArgb((int)((1 - row.Appear) * 255), BackColor))) g.FillRectangle(veil, r.X, r.Y, r.Width, r.Height + 1);
            }
        PaintScrollbar(g);
    }
}
