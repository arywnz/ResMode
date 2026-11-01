using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

public static class Kinds {
    public static Color Color(LogKind k) {
        return k == LogKind.Game ? Theme.Live : k == LogKind.Library ? Theme.Brand : k == LogKind.Error ? Theme.Danger : System.Drawing.Color.FromArgb(160, 166, 180);
    }
    public static string Label(LogKind k) { return k == LogKind.Game ? "Jogo" : k == LogKind.Library ? "Biblioteca" : k == LogKind.Error ? "Erro" : "Sistema"; }
    public static string Glyph(LogKind k) { return k == LogKind.Game ? "" : k == LogKind.Library ? "" : k == LogKind.Error ? "" : ""; }

    // Coloured pill with the kind's name, e.g. a green "Jogo".
    public static int Pill(Graphics g, LogKind k, Font f, int x, int cy) {
        string text = Label(k);
        int w = TextRenderer.MeasureText(text, f, Size.Empty, TextFormatFlags.NoPadding).Width + Theme.S(16), h = Theme.S(22);
        var color = Color(k);
        Theme.Fill(g, new RectangleF(x, cy - h / 2f, w, h), h / 2f, Theme.Mix(Theme.Surface, color, 0.2f));
        TextRenderer.DrawText(g, text, f, new Rectangle(x, cy - h / 2, w, h), Theme.Mix(color, System.Drawing.Color.White, 0.25f), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return w;
    }
}

// History list: day headings, one divided row per entry with a coloured kind pill; repeats fold into "3×".
public class LogView : ScrollSurface {
    public class Entry { public DateTime Time; public string Text; public LogKind Kind; public int Repeats = 1; }
    readonly List<object> rows = new List<object>(); // string = day heading, Entry = line
    public string EmptyText = "Histórico vazio.";
    int RowH { get { return Theme.S(46); } }
    int HeadH { get { return Theme.S(46); } }

    public LogView() { Font = Theme.Body(10.5f); }

    public static Entry Parse(string line) {
        DateTime t;
        if (line.Length < 21 || !DateTime.TryParseExact(line.Substring(0, 19), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return null;
        string rest = line.Substring(20);
        var kind = LogKind.System;
        if (rest.StartsWith("[")) {
            int end = rest.IndexOf(']');
            string tag = end > 0 ? rest.Substring(1, end - 1) : "";
            for (int i = 0; i < 4; i++) if (Store.Tag((LogKind)i) == tag) kind = (LogKind)i;
            rest = end > 0 ? rest.Substring(end + 1).TrimStart() : rest;
        } else if (rest.StartsWith("Erro") || rest.StartsWith("Falha") || rest.Contains("Exception")) kind = LogKind.Error;
        return new Entry { Time = t, Text = rest, Kind = kind };
    }

    public void SetEntries(IEnumerable<Entry> newestFirst) {
        rows.Clear();
        DateTime day = DateTime.MinValue;
        Entry last = null;
        var culture = new CultureInfo("pt-BR");
        foreach (var src in newestFirst) {
            if (last != null && last.Text == src.Text && last.Kind == src.Kind && last.Time.Date == src.Time.Date) { last.Repeats++; continue; }
            var e = new Entry { Time = src.Time, Text = src.Text, Kind = src.Kind };
            if (e.Time.Date != day) {
                day = e.Time.Date;
                string label = day == DateTime.Today ? "Hoje" : day == DateTime.Today.AddDays(-1) ? "Ontem" : day.ToString("dddd, d 'de' MMMM", culture);
                rows.Add(Char.ToUpper(label[0]) + label.Substring(1));
            }
            rows.Add(e);
            last = e;
        }
        content = rows.Sum(r => r is string ? HeadH : RowH);
        Clamp();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.Smooth(g);
        if (rows.Count == 0) {
            Theme.DrawText(g, EmptyText, Font, Theme.Muted, new Rectangle(0, Theme.S(8), Width, Theme.S(30)));
            return;
        }
        int y = -(int)offset, w = Width - Theme.S(14);
        using (var head = Theme.Body(11, true)) using (var time = Theme.Mono(10)) using (var small = Theme.Body(9, true)) using(var repeat=Theme.Mono(9.5f,true)) using (var line = new Pen(Theme.Border))
            for (int i = 0; i < rows.Count; i++) {
                var heading = rows[i] as string;
                int rh = heading != null ? HeadH : RowH;
                if (y + rh >= 0 && y <= Height) {
                    if (heading != null) {
                        Theme.DrawText(g, heading, head, Theme.Text, new Rectangle(0, y + Theme.S(12), w, Theme.S(26)));
                    } else {
                        var entry = (Entry)rows[i];
                        int cy = y + rh / 2;
                        Theme.DrawText(g, entry.Time.ToString("HH:mm"), time, Theme.Muted, new Rectangle(0, y, Theme.S(48), rh));
                        int pw = Kinds.Pill(g, entry.Kind, small, Theme.S(52), cy);
                        int tx = Theme.S(52) + Math.Max(pw, Theme.S(86)) + Theme.S(12), tw = w - tx;
                        if (entry.Repeats > 1) {
                            string n = entry.Repeats + "×";
                            int cw = TextRenderer.MeasureText(n, repeat).Width + Theme.S(8);
                            Theme.Fill(g, new RectangleF(w - cw, cy - Theme.S(11), cw, Theme.S(22)), Theme.S(11), Theme.Raised);
                            Theme.DrawText(g, n, repeat, Theme.Muted, new Rectangle(w - cw, y, cw, rh), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                            tw -= cw + Theme.S(8);
                        }
                        Theme.DrawText(g, entry.Text, Font, Theme.Text, new Rectangle(tx, y, tw, rh));
                        g.DrawLine(line, 0, y + rh - 1, w, y + rh - 1);
                    }
                }
                y += rh;
            }
        PaintScrollbar(g);
    }
}

// Bell with an unread badge.
public class BellButton : FlatButton {
    public int Unread;
    public BellButton() { Glyph = ""; Kind = ButtonKind.Quiet; Width = Theme.S(36); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if (Unread <= 0) return;
        var g = e.Graphics;
        Theme.Smooth(g);
        float d = Theme.S(9);
        using (var ring = new SolidBrush(ParentColor)) g.FillEllipse(ring, Width - Theme.S(13), Theme.S(5), d + Theme.S(3), d + Theme.S(3));
        using (var dot = new SolidBrush(Theme.Accent)) g.FillEllipse(dot, Width - Theme.S(11.5f), Theme.S(6.5f), d, d);
    }
}

// Refresh icon that spins while the library is being read, then shows a green check for a moment.
public class RefreshButton : FlatButton {
    readonly System.Windows.Forms.Timer spin = new System.Windows.Forms.Timer { Interval = 16 };
    Func<bool> busy;
    DateTime started;
    float angle, done;
    public bool Spinning { get { return spin.Enabled; } }

    public RefreshButton() {
        Glyph = "";
        Width = Theme.S(36);
        spin.Tick += delegate {
            angle = (angle + 9) % 360;
            if (!busy() && (DateTime.Now - started).TotalMilliseconds > 700) {
                spin.Stop();
                angle = 0;
                Motion.Run(Tuple.Create(this, "done"), 1600, e => { done = e < 0.15f ? e / 0.15f : e > 0.8f ? (1 - e) / 0.2f : 1; Invalidate(); });
            }
            Invalidate();
        };
    }

    public void Start(Func<bool> isBusy) {
        busy = isBusy;
        started = DateTime.Now;
        done = 0;
        spin.Start();
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var f = Theme.Glyphs(11)) using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) {
            var state = g.Save();
            g.TranslateTransform(Width / 2f, Height / 2f);
            if (done > 0) {
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * done), Theme.Live))) g.DrawString(Theme.IconCheck, f, b, 0, Theme.S(1), fmt);
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * (1 - done)), Theme.Text))) g.DrawString(Theme.IconRefresh, f, b, 0, Theme.S(1), fmt);
            } else {
                g.RotateTransform(angle);
                using (var b = new SolidBrush(Spinning ? Theme.Accent : Theme.Text)) g.DrawString(Theme.IconRefresh, f, b, 0, Theme.S(1), fmt);
            }
            g.Restore(state);
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) spin.Dispose();
        base.Dispose(disposing);
    }
}

// Notification dropdown under the bell: one card per notice, coloured icon by kind, "Limpar tudo".
public class NoticeList : Painted {
    readonly Func<IList<AppContext.Notice>> getNotices;
    readonly Action clearNotices;
    readonly FlatButton clear;
    readonly NoticeBody body;
    readonly System.Windows.Forms.Timer updates;
    int maximumHeight;
    bool arranging;
    string signature = "";
    int count;
    int HeaderHeight { get { return Theme.S(68); } }

    public NoticeList(AppContext owner) : this(() => owner.Notices, owner.ClearNotices) { }

    public NoticeList(Func<IList<AppContext.Notice>> getNotices, Action clearNotices) {
        this.getNotices = getNotices;
        this.clearNotices = clearNotices;
        BackColor = Theme.Raised;
        Font = Theme.Body(10);
        maximumHeight = Theme.S(540);
        Width = Theme.S(460);
        clear = new FlatButton { Text = "Limpar tudo", Kind = ButtonKind.Secondary, Height = Theme.S(30), Font = Theme.Body(9.5f, true), AccessibleName = "Limpar todas as notificações" };
        clear.Click += delegate { this.clearNotices(); RefreshNotices(); };
        body = new NoticeBody { BackColor = BackColor, AccessibleName = "Lista de notificações", AccessibleRole = AccessibleRole.List };
        Controls.Add(body);
        Controls.Add(clear);
        updates = new System.Windows.Forms.Timer { Interval = 500 };
        updates.Tick += delegate { RefreshNotices(); };
        VisibleChanged += delegate { if (Visible) { RefreshNotices(); updates.Start(); } else updates.Stop(); };
        RefreshNotices();
    }

    public void SetViewport(int width, int height) {
        maximumHeight = Math.Max(HeaderHeight + Theme.S(60), height);
        Width = Math.Max(Theme.S(180), width);
        Arrange();
    }

    public void FocusContent() { body.Focus(); }

    void RefreshNotices() {
        var notices = getNotices().ToList();
        string next = String.Join("|", notices.Select(n => n.Time.Ticks + ":" + n.Title + ":" + n.Text + ":" + n.Kind));
        if (next == signature && count == notices.Count && body.Initialized) return;
        signature = next;
        count = notices.Count;
        clear.Enabled = count > 0;
        body.SetNotices(notices);
        Arrange();
        Invalidate();
    }

    void Arrange() {
        if (arranging || body == null) return;
        arranging = true;
        try {
            int pad = Theme.S(16), edge = Theme.S(8);
            clear.Width = clear.PreferredWidth();
            clear.Location = new Point(Width - pad - clear.Width, Theme.S(15));
            int bodyWidth = Math.Max(1, Width - edge * 2);
            int wanted = body.MeasureContent(bodyWidth);
            Height = Math.Min(maximumHeight, HeaderHeight + wanted + edge);
            body.Bounds = new Rectangle(edge, HeaderHeight, bodyWidth, Math.Max(1, Height - HeaderHeight - edge));
        } finally { arranging = false; }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Arrange(); }
    protected override void OnMouseWheel(MouseEventArgs e) { body.ScrollWheel(e); base.OnMouseWheel(e); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        int pad = Theme.S(16);
        using (var head = Theme.Body(12, true))
            Theme.DrawText(g, "Notificações", head, Theme.Text, new Rectangle(pad, Theme.S(9), Math.Max(1, clear.Left - pad - Theme.S(10)), Theme.S(27)));
        using (var small = Theme.Body(9))
            Theme.DrawText(g, count == 0 ? "Você está em dia" : count == 1 ? "1 notificação nesta sessão" : count + " notificações nesta sessão", small, Theme.Muted, new Rectangle(pad, Theme.S(36), Width - pad * 2, Theme.S(22)));
        using (var pen = new Pen(Theme.Border))
            g.DrawLine(pen, Theme.S(10), HeaderHeight - Theme.S(4), Width - Theme.S(10), HeaderHeight - Theme.S(4));
    }

    protected override void Dispose(bool disposing) {
        if (disposing && updates != null) updates.Dispose();
        base.Dispose(disposing);
    }

    sealed class NoticeBody : ScrollSurface {
        sealed class Row {
            public AppContext.Notice Notice;
            public int Top, Height, TitleHeight, TextHeight;
        }
        readonly List<Row> rows = new List<Row>();
        List<AppContext.Notice> notices = new List<AppContext.Notice>();
        int measuredWidth = -1;
        public bool Initialized { get; private set; }
        const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl;
        int TextLeft { get { return Theme.S(56); } }

        public NoticeBody() { Font = Theme.Body(10); }
        public void SetNotices(List<AppContext.Notice> items) {
            notices = items;
            Initialized = true;
            measuredWidth = -1;
            ScrollTop();
        }

        public int MeasureContent(int width) {
            if (width == measuredWidth) return content;
            measuredWidth = width;
            rows.Clear();
            int y = Theme.S(8), textWidth = Math.Max(1, width - TextLeft - Theme.S(24));
            using (var title = Theme.Body(10.5f, true))
            using (var time = Theme.Mono(9.5f)) {
                foreach (var notice in notices) {
                    int titleHeight = TextRenderer.MeasureText(notice.Title ?? "", title, new Size(textWidth, Int32.MaxValue), Wrap).Height;
                    int textHeight = String.IsNullOrEmpty(notice.Text) ? 0 : TextRenderer.MeasureText(notice.Text, Font, new Size(textWidth, Int32.MaxValue), Wrap).Height;
                    int height = Theme.S(14) + titleHeight + (textHeight > 0 ? Theme.S(6) + textHeight : 0) + Theme.S(10) + time.Height + Theme.S(14);
                    rows.Add(new Row { Notice = notice, Top = y, Height = height, TitleHeight = titleHeight, TextHeight = textHeight });
                    y += height + Theme.S(10);
                }
            }
            content = notices.Count == 0 ? Theme.S(148) : y;
            Clamp();
            Invalidate();
            return content;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); MeasureContent(Width); Clamp(); }
        public void ScrollWheel(MouseEventArgs e) { base.OnMouseWheel(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override bool IsInputKey(Keys key) {
            switch (key & Keys.KeyCode) {
                case Keys.Up: case Keys.Down: case Keys.PageUp: case Keys.PageDown: case Keys.Home: case Keys.End: return true;
            }
            return base.IsInputKey(key);
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            int delta = 0;
            switch (e.KeyCode) {
                case Keys.Up: delta = 40; break;
                case Keys.Down: delta = -40; break;
                case Keys.PageUp: delta = Math.Max(120, Height * 120 / Math.Max(1, Theme.S(90))); break;
                case Keys.PageDown: delta = -Math.Max(120, Height * 120 / Math.Max(1, Theme.S(90))); break;
                case Keys.Home: delta = Int16.MaxValue; break;
                case Keys.End: delta = -Int16.MaxValue; break;
            }
            if (delta != 0) { ScrollWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta)); e.Handled = e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            if (notices.Count == 0) {
                using (var icon = Theme.Glyphs(24))
                    Theme.DrawGlyph(g, "\uEA8F", icon, Theme.Faint, new Rectangle(0, Theme.S(22), Width, Theme.S(48)));
                Theme.DrawText(g, "Sem notificações", Font, Theme.Muted, new Rectangle(0, Theme.S(82), Width, Theme.S(28)), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            int textWidth = Math.Max(1, Width - TextLeft - Theme.S(24));
            using (var title = Theme.Body(10.5f, true))
            using (var time = Theme.Mono(9.5f))
            using (var glyph = Theme.Glyphs(11)) {
                foreach (var row in rows) {
                    int y = row.Top - (int)offset;
                    if (y + row.Height < 0 || y > Height) continue;
                    var card = new Rectangle(0, y, Width - Theme.S(12), row.Height);
                    Theme.Fill(g, card, Theme.S(10), Theme.Surface);
                    var color = Kinds.Color(row.Notice.Kind);
                    var circle = new Rectangle(Theme.S(12), y + Theme.S(14), Theme.S(30), Theme.S(30));
                    using (var brush = new SolidBrush(Theme.Mix(Theme.Surface, color, .2f))) g.FillEllipse(brush, circle);
                    Theme.DrawGlyph(g, Kinds.Glyph(row.Notice.Kind), glyph, color, circle);
                    int textY = y + Theme.S(14);
                    TextRenderer.DrawText(g, row.Notice.Title ?? "", title, new Rectangle(TextLeft, textY, textWidth, row.TitleHeight), Theme.Text, Wrap);
                    textY += row.TitleHeight;
                    if (row.TextHeight > 0) {
                        textY += Theme.S(6);
                        TextRenderer.DrawText(g, row.Notice.Text, Font, new Rectangle(TextLeft, textY, textWidth, row.TextHeight), Theme.Muted, Wrap);
                        textY += row.TextHeight;
                    }
                    var n = row.Notice;
                    string when = n.Time.Date == DateTime.Today ? "Hoje às " + n.Time.ToString("HH:mm") : n.Time.ToString("dd/MM 'às' HH:mm");
                    TextRenderer.DrawText(g, when, time, new Rectangle(TextLeft, textY + Theme.S(10), textWidth, time.Height), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
            }
            PaintScrollbar(g);
        }
    }
}

// The big "Jogar": accent gradient, a drawn triangle, light that follows the pointer, and a small give when pressed.
// While the game is running it turns into a calm "Em jogo" with a live dot.
public class PlayButton : FlatButton {
    public bool Live;
    float press;
    public PlayButton() { Height = Theme.S(48); Font = Theme.Body(11.5f, true); }
    protected override void OnMouseDown(MouseEventArgs e) { Motion.To(Tuple.Create(this, "press"), press, 1, Motion.Fast, v => { press = v; Invalidate(); }); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Motion.To(Tuple.Create(this, "press"), press, 0, Motion.Normal, v => { press = v; Invalidate(); }); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(ParentColor);
        Theme.Smooth(g);
        float inset = Theme.S(2) * press;
        var r = new RectangleF(0.5f + inset, 0.5f + inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
        float radius = Theme.S(10);
        int textW = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        using (var path = Theme.Round(r, radius)) {
            if (!Enabled) {
                using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
                int dot = Live ? Theme.S(8) : 0, gap = Live ? Theme.S(10) : 0, x = (Width - dot - gap - textW) / 2;
                if (Live) using (var b = new SolidBrush(Theme.Live)) g.FillEllipse(b, x, (Height - dot) / 2f, dot, dot);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x + dot + gap, 0, textW + Theme.S(4), Height), Live ? Theme.Text : Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                return;
            }
            Color top = Theme.Mix(Theme.Accent, Color.White, 0.35f + 0.35f * hoverT), bottom = Theme.Mix(Theme.Accent, Color.FromArgb(190, 195, 205), 0.6f - 0.35f * hoverT);
            using (var b = new LinearGradientBrush(r, top, bottom, 90f)) g.FillPath(b, path);
            if (hoverT > 0 && mouse != null) using (var light = Theme.Light(mouse.Value, Width * 0.6f, (int)(80 * hoverT))) g.FillPath(light, path);
            // A thin bright rim on top reads as a raised, glassy surface.
            using (var rim = new LinearGradientBrush(r, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) using (var p = new Pen(rim, Theme.S(1))) g.DrawPath(p, path);
            FocusRing(g, r, radius);

            int tri = Theme.S(12), space = Theme.S(10);
            float x0 = (Width - tri - space - textW) / 2f, cy = Height / 2f;
            var points = new[] { new PointF(x0, cy - tri * 0.58f), new PointF(x0 + tri, cy), new PointF(x0, cy + tri * 0.58f) };
            using (var b = new SolidBrush(Theme.AccentInk)) g.FillPolygon(b, points);
            using (var p = new Pen(Theme.AccentInk, Theme.S(2)) { LineJoin = LineJoin.Round }) g.DrawPolygon(p, points);
            TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(x0 + tri + space), 0, textW + Theme.S(4), Height), Theme.AccentInk, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}

// Opens when a game card is clicked: cover and name, when it was last played and for how long,
// a large "Jogar", then the quieter actions.
public class GamePanel : Painted {
    public readonly PlayButton Play;
    public readonly FlatButton Toggle, Folder;
    readonly LibraryGame game;
    readonly Playtime.Info time;
    SteamAchievements.Progress achievements=new SteamAchievements.Progress{Message="Sincronizando com a Steam…"};
    readonly System.Windows.Forms.Timer achievementSync;
    bool achievementReadPending;
    Rectangle Thumb { get { return new Rectangle(Theme.S(18), Theme.S(18), Theme.S(64), Theme.S(96)); } }
    Rectangle Stats { get { return new Rectangle(Theme.S(18), Thumb.Bottom + Theme.S(16), Width - Theme.S(36), Theme.S(58)); } }
    Rectangle Achievements {get{return new Rectangle(Theme.S(18),Stats.Bottom+Theme.S(12),Width-Theme.S(36),Theme.S(76));}}

    public GamePanel(LibraryGame game, bool running) {
        this.game = game;
        time = Playtime.Get(game.Key);
        BackColor = Theme.Raised;
        Width = Theme.S(330);
        Play = new PlayButton { Kind = ButtonKind.Primary, Live = running, Text = running ? "Em jogo" : game.Installed ? "Jogar" : "Não encontrado", Enabled = game.Installed && !running };
        Toggle = new FlatButton { Kind = ButtonKind.Quiet, AlignLeft = true, Height = Theme.S(38), Font = Theme.Body(10.5f),
            Glyph = game.Excluded ? Theme.IconCheck : Theme.IconPause, Text = game.Excluded ? "Usar resolução de jogo" : "Ignorar este jogo" };
        Folder = new FlatButton { Kind = ButtonKind.Quiet, AlignLeft = true, Height = Theme.S(38), Font = Theme.Body(10.5f), Glyph = Theme.IconFolder, Text = "Abrir pasta" };
        Play.SetBounds(Theme.S(18), Achievements.Bottom + Theme.S(14), Width - Theme.S(36), Play.Height);
        Toggle.SetBounds(Theme.S(8), Play.Bottom + Theme.S(17), Width - Theme.S(16), Toggle.Height);
        Folder.SetBounds(Theme.S(8), Toggle.Bottom + Theme.S(2), Width - Theme.S(16), Folder.Height);
        Height = Folder.Bottom + Theme.S(10);
        Controls.AddRange(new Control[] { Play, Toggle, Folder });
        achievementSync=new System.Windows.Forms.Timer{Interval=2000};achievementSync.Tick+=delegate{ReadAchievements();};
        VisibleChanged+=delegate{if(Visible){ReadAchievements();achievementSync.Start();}else achievementSync.Stop();};
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ReadAchievements();}
    void ReadAchievements(){
        if(achievementReadPending||IsDisposed||!IsHandleCreated)return;achievementReadPending=true;
        System.Threading.Tasks.Task.Factory.StartNew(()=>SteamAchievements.Get(game.Key)).ContinueWith(task=>{
            if(IsDisposed||!IsHandleCreated)return;
            try{BeginInvoke(new Action(delegate{if(IsDisposed)return;achievementReadPending=false;if(task.IsFaulted){var observed=task.Exception;achievements=new SteamAchievements.Progress();}else achievements=task.Result;Invalidate(Achievements);}));}catch(InvalidOperationException){}
        });
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.Smooth(g);
        var thumb = Thumb;
        var art = Artwork.Get(game.Key);
        // A faint wash of the cover's colour behind the header ties the panel to the game.
        if (art != null) {
            var band = new Rectangle(0, 0, Width, thumb.Bottom + Theme.S(30));
            using (var wash = new LinearGradientBrush(band, Color.FromArgb(38, art.Tint), Color.FromArgb(0, art.Tint), 90f)) g.FillRectangle(wash, band);
        }
        using (var path = Theme.Round(thumb, Theme.S(8))) {
            using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (art != null && art.Cover != null) g.DrawImage(art.Cover, thumb);
            else if (art != null && art.Icon != null) { int s = Theme.S(36); g.DrawImage(art.Icon, thumb.X + (thumb.Width - s) / 2, thumb.Y + (thumb.Height - s) / 2, s, s); }
            using (var p = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(p, path);
        }

        int x = thumb.Right + Theme.S(16), w = Width - x - Theme.S(16);
        using (var name = Theme.Body(13, true)) using (var small = Theme.Body(10)) {
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            int h = Math.Min(TextRenderer.MeasureText(game.Name, name, new Size(w, 0), flags).Height, Theme.S(52));
            TextRenderer.DrawText(g, game.Name, name, new Rectangle(x, thumb.Y + Theme.S(4), w, h), Theme.Text, flags);
            int y = thumb.Y + Theme.S(10) + h, lx = x;
            var icon = game.Launcher.Icon;
            if (icon != null) { g.DrawImage(icon, lx, y + Theme.S(2), Theme.S(14), Theme.S(14)); lx += Theme.S(20); }
            Theme.DrawText(g, game.Launcher.Name, small, Theme.Muted, new Rectangle(lx, y, Width - lx - Theme.S(14), Theme.S(18)));
            if (game.Excluded)
                Theme.DrawText(g, game.Competitive ? "Competitivo, fica na resolução nativa" : "Fica na resolução nativa", small, Theme.Warn,
                    new Rectangle(x, y + Theme.S(22), w, Theme.S(36)), TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }

        // Two figures side by side on a recessed strip.
        var stats = Stats;
        Theme.Fill(g, stats, Theme.S(10), Theme.Surface);
        int half = stats.Width / 2;
        using (var p = new Pen(Theme.Border)) g.DrawLine(p, stats.X + half, stats.Y + Theme.S(12), stats.X + half, stats.Bottom - Theme.S(12));
        using (var label = Theme.Body(10)) using (var value = Theme.Body(12, true)) using(var number=Theme.Mono(11.5f,true)) {
            Theme.DrawText(g, "Última vez", label, Theme.Faint, new Rectangle(stats.X + Theme.S(14), stats.Y + Theme.S(9), half - Theme.S(20), Theme.S(18)));
            Theme.DrawText(g, Playtime.FormatLast(time), value, time.Running ? Theme.Live : Theme.Text, new Rectangle(stats.X + Theme.S(14), stats.Y + Theme.S(27), half - Theme.S(20), Theme.S(22)));
            Theme.DrawText(g, "Tempo jogado", label, Theme.Faint, new Rectangle(stats.X + half + Theme.S(14), stats.Y + Theme.S(9), half - Theme.S(20), Theme.S(18)));
            Theme.DrawText(g, Playtime.FormatTotal(time.Total), time.Total.TotalMinutes>=1?number:value, Theme.Text, new Rectangle(stats.X + half + Theme.S(14), stats.Y + Theme.S(27), half - Theme.S(20), Theme.S(22)));
        }
        var progress=Achievements;Theme.Fill(g,progress,Theme.S(10),Theme.Surface);
        using(var label=Theme.Body(10,true))using(var number=Theme.Mono(10.5f,true))using(var caption=Theme.Body(9.5f)){
            Theme.DrawText(g,"Conquistas",label,Theme.Text,new Rectangle(progress.X+Theme.S(14),progress.Y+Theme.S(8),Theme.S(130),Theme.S(23)));
            if(achievements.Available&&achievements.Total>0){
                Theme.DrawText(g,achievements.Unlocked+" / "+achievements.Total,number,Theme.Text,new Rectangle(progress.X+Theme.S(142),progress.Y+Theme.S(8),progress.Width-Theme.S(156),Theme.S(23)),TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
                var bar=new RectangleF(progress.X+Theme.S(14),progress.Y+Theme.S(36),progress.Width-Theme.S(28),Theme.S(4));Theme.Fill(g,bar,Theme.S(2),Theme.Border);
                if(achievements.Unlocked>0)Theme.Fill(g,new RectangleF(bar.X,bar.Y,bar.Width*achievements.Unlocked/achievements.Total,bar.Height),Theme.S(2),Theme.Text);
            }
            Theme.DrawText(g,achievements.Message,caption,Theme.Muted,new Rectangle(progress.X+Theme.S(14),progress.Y+Theme.S(46),progress.Width-Theme.S(28),Theme.S(22)));
        }
        using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, Play.Bottom + Theme.S(8), Width, Play.Bottom + Theme.S(8));
    }
    protected override void Dispose(bool disposing){if(disposing&&achievementSync!=null)achievementSync.Dispose();base.Dispose(disposing);}
}

// Short explanation card: a title, then paragraphs; a paragraph may have a heading with a coloured dot.
public class InfoCard : Painted {
    public class Block { public string Heading, Body; public Color Dot; }
    readonly string title;
    readonly List<Block> blocks;
    readonly Font titleFont = Theme.Body(12, true), headFont = Theme.Body(10.5f, true), bodyFont = Theme.Body(10);
    const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    int Pad { get { return Theme.S(18); } }

    public InfoCard(string title, int width, params Block[] blocks) {
        this.title = title;
        this.blocks = blocks.ToList();
        BackColor = Theme.Raised;
        Width = width;
        Height = Measure(null);
    }

    // One pass both measures and draws, so the two can never disagree.
    int Measure(Graphics g) {
        int w = Width - Pad * 2, y = Pad;
        if (g != null) TextRenderer.DrawText(g, title, titleFont, new Point(Pad, y), Theme.Text, TextFormatFlags.NoPadding);
        y += TextRenderer.MeasureText(title, titleFont).Height + Theme.S(8);
        foreach (var b in blocks) {
            y += Theme.S(8);
            if (b.Heading != null) {
                int hh = TextRenderer.MeasureText(b.Heading, headFont).Height;
                if (g != null) {
                    int d = Theme.S(8);
                    using (var dot = new SolidBrush(b.Dot)) g.FillEllipse(dot, Pad, y + (hh - d) / 2, d, d);
                    TextRenderer.DrawText(g, b.Heading, headFont, new Point(Pad + Theme.S(16), y), Theme.Text, TextFormatFlags.NoPadding);
                }
                y += hh + Theme.S(4);
            }
            int bh = TextRenderer.MeasureText(b.Body, bodyFont, new Size(w, 0), Wrap).Height;
            if (g != null) TextRenderer.DrawText(g, b.Body, bodyFont, new Rectangle(Pad, y, w, bh), Theme.Muted, Wrap);
            y += bh;
        }
        return y + Pad;
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        Theme.Smooth(e.Graphics);
        Measure(e.Graphics);
    }
}
