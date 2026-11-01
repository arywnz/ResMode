using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Base for every custom control: double-buffered, with an animated hover value (0..1).
public class Painted : Control {
    protected float hoverT;
    public Painted() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Theme.Base;
        ForeColor = Theme.Text;
    }
    void Hover(float target) { Motion.To(Tuple.Create(this, "hover"), hoverT, target, Motion.Fast, v => { hoverT = v; Invalidate(); }); }
    protected override void OnMouseEnter(EventArgs e) { Hover(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Hover(0); Press(0); base.OnMouseLeave(e); }

    // Pressed state (0..1), for controls that opt in: it sinks the button, stretches a switch's thumb, etc.
    protected float pressT;
    protected virtual bool TracksPress { get { return false; } }
    void Press(float target) {
        if (!TracksPress || pressT == target) return;
        Motion.To(Tuple.Create(this, "pressed"), pressT, target, target > 0 ? Motion.Fast : Motion.Normal, v => { pressT = v; Invalidate(); });
    }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Press(1); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Press(0); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected Color ParentColor { get { return Parent != null ? Parent.BackColor : Theme.Base; } }
    protected void FocusRing(Graphics g, RectangleF r, float radius) {
        if (Focused && ShowFocusCues) using (var path = Theme.Round(r, radius)) using (var p = new Pen(Theme.Accent, Theme.S(1.5f))) g.DrawPath(p, path);
    }

    // Windows 10 "Reveal": controls that opt in get a soft light under the pointer and an edge lit near it,
    // fading with the hover. The light stays where the pointer left, so it fades out in place.
    protected Point? mouse;
    protected virtual bool Reveals { get { return false; } }
    protected override void OnMouseMove(MouseEventArgs e) {
        if (Reveals) { mouse = e.Location; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected void Reveal(Graphics g, GraphicsPath shape, int gloss = 30, int edge = 150) {
        if (!Enabled || hoverT <= 0 || mouse == null) return;
        float reach = Math.Max(Width, Height) * 0.9f;
        if (gloss > 0) using (var light = Theme.Light(mouse.Value, reach, (int)(gloss * hoverT))) g.FillPath(light, shape);
        if (edge > 0) using (var light = Theme.Light(mouse.Value, reach, (int)(edge * hoverT))) using (var pen = new Pen(light, Theme.S(1.2f))) g.DrawPath(pen, shape);
    }
}

public enum ButtonKind { Primary, Secondary, Quiet }

public class FlatButton : Painted {
    public ButtonKind Kind = ButtonKind.Secondary;
    public Color Ink = Color.Empty; // text colour override for quiet buttons (e.g. a red "Sair")
    public string Glyph;
    public bool AlignLeft; // menu-style rows: glyph and text from the left edge
    protected override bool Reveals { get { return true; } }
    protected override bool TracksPress { get { return true; } }
    public FlatButton() { Cursor = Cursors.Hand; TabStop = true; Font = Theme.Body(10, true); Height = Theme.S(36); }
    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; base.OnEnabledChanged(e); }
    public int PreferredWidth() {
        bool hasText = !String.IsNullOrEmpty(Text), hasGlyph = !String.IsNullOrEmpty(Glyph);
        if (!hasText) return Height;
        return TextRenderer.MeasureText(Text, Font).Width + Theme.S(28) + (hasGlyph ? Theme.S(24) : 0);
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(ParentColor);
        Theme.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        float radius = Theme.S(9);
        Color fill, ink;
        if (Kind == ButtonKind.Primary) { fill = Theme.Mix(Theme.Accent, Color.White, 0.18f * hoverT); ink = Theme.AccentInk; }
        else if (Kind == ButtonKind.Secondary) { fill = Theme.Mix(Theme.Raised, Color.FromArgb(34, 37, 46), hoverT); ink = Theme.Text; }
        else { fill = Theme.Mix(ParentColor, Theme.Raised, hoverT); ink = Ink != Color.Empty ? Ink : Theme.Mix(Theme.Muted, Theme.Text, hoverT); }
        if (!Enabled) { fill = Kind == ButtonKind.Quiet ? ParentColor : Theme.Surface; ink = Theme.Faint; }
        fill = Theme.Mix(fill, Color.Black, 0.16f * pressT);
        bool raised = Enabled && Kind != ButtonKind.Quiet;
        using (var path = Theme.Round(r, radius)) {
            if (raised) {
                // A soft top-to-bottom gradient and a bright hairline along the top edge give the button some body.
                Color top = Theme.Mix(fill, Color.White, Kind == ButtonKind.Primary ? 0.12f : 0.045f), bottom = Theme.Mix(fill, Color.Black, Kind == ButtonKind.Primary ? 0.06f : 0.1f);
                using (var body = new LinearGradientBrush(new RectangleF(0, 0, Width, Height + 1), top, bottom, 90f)) g.FillPath(body, path);
                using (var sheen = new Pen(Color.FromArgb(Kind == ButtonKind.Primary ? 110 : 22, 255, 255, 255))) g.DrawLine(sheen, radius * 0.8f, 1.5f, Width - radius * 0.8f, 1.5f);
            } else {
                using (var flat = new SolidBrush(fill)) g.FillPath(flat, path);
            }
            if (Kind == ButtonKind.Secondary) Theme.Stroke(g, r, radius, Enabled ? Theme.Mix(Theme.Border, Color.FromArgb(70, 75, 88), hoverT) : Theme.Border);
            // White light would vanish on the platinum primary button; that one brightens through its fill instead.
            if (Kind != ButtonKind.Primary) Reveal(g, path);
        }
        FocusRing(g, r, radius);

        int dy = (int)Math.Round(Theme.S(1) * pressT); // the label sinks a hair with the button
        bool hasText = !String.IsNullOrEmpty(Text), hasGlyph = !String.IsNullOrEmpty(Glyph);
        if (!hasText) { using (var f = Theme.Glyphs(10.5f)) Theme.DrawGlyph(g, Glyph, f, ink, new Rectangle(0, dy, Width, Height)); return; }
        int glyphW = hasGlyph ? Theme.S(16) : 0, gap = hasGlyph ? Theme.S(8) : 0;
        int textW = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        int x = AlignLeft ? Theme.S(12) : (Width - glyphW - gap - textW) / 2;
        if (hasGlyph) using (var f = Theme.Glyphs(10.5f)) Theme.DrawGlyph(g, Glyph, f, ink, new Rectangle(x, dy, glyphW, Height));
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x + glyphW + gap, dy, textW + Theme.S(4), Height), ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}

public class ToggleSwitch : Painted {
    bool on;
    float onT;
    public event EventHandler CheckedChanged;
    protected override bool Reveals { get { return true; } }
    protected override bool TracksPress { get { return true; } }
    public ToggleSwitch() { Size = new Size(Theme.S(42), Theme.S(24)); Cursor = Cursors.Hand; TabStop = true; }
    public bool Checked {
        get { return on; }
        set {
            if (on == value) return;
            on = value;
            // Before the switch is on screen (a modal being built) there is nothing to animate: start in place.
            if (!IsHandleCreated || !Visible) { onT = on ? 1 : 0; return; }
            Motion.To(Tuple.Create(this, "on"), onT, on ? 1 : 0, Motion.Normal, v => { onT = v; Invalidate(); });
        }
    }
    void Flip() {
        Checked = !on;
        if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
    }
    protected override void OnClick(EventArgs e) { Flip(); base.OnClick(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Flip(); base.OnKeyDown(e); }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(ParentColor);
        Theme.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        var off = Theme.Mix(Color.FromArgb(36, 39, 47), Color.FromArgb(46, 49, 58), hoverT);
        Theme.Fill(g, r, Height / 2f, Theme.Mix(off, Theme.Accent, onT));
        using (var path = Theme.Round(r, Height / 2f)) Reveal(g, path, (int)(30 * (1 - onT)), 150);
        FocusRing(g, r, Height / 2f);
        // The thumb stretches while pressed, like Windows 11's switches.
        float d = Height - Theme.S(8), stretch = d * 0.3f * pressT;
        float x = Theme.S(4) + (Width - d - stretch - Theme.S(8)) * onT;
        Theme.Fill(g, new RectangleF(x, Theme.S(4), d + stretch, d), d / 2f, Theme.Mix(Theme.Muted, Theme.AccentInk, onT));
    }
}

// Items can give the dropdown a main label and a quieter detail shown on the right.
public interface IListItem { string Label { get; } string Detail { get; } }

// Dropdown: the box, plus a list that slides open below it (or above, near the screen edge).
public class SelectBox : FlatButton {
    public bool Technical;
    public List<object> Items = new List<object>();
    object selected;
    float openT;
    public event EventHandler SelectionCommitted;
    public SelectBox() { Font = Theme.Body(10); }
    public object SelectedItem {
        get { return selected; }
        set { selected = value; Text = value == null ? "" : value.ToString(); Invalidate(); }
    }
    void Opened(bool open) { Motion.To(Tuple.Create(this, "open"), openT, open ? 1 : 0, Motion.Normal, v => { openT = v; Invalidate(); }); }
    protected override void OnClick(EventArgs e) {
        if (Items.Count == 0) return;
        var list = new SelectList(Items, selected, Width, Technical);
        var drop = Theme.Popover(list, list.Reveal);
        list.Picked += item => {
            drop.Close();
            SelectedItem = item;
            if (SelectionCommitted != null) SelectionCommitted(this, EventArgs.Empty);
        };
        drop.Closed += delegate { Opened(false); };
        Opened(true);
        Theme.ShowNear(drop, this, Theme.S(6), true);
        list.Focus();
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(ParentColor);
        Theme.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        Theme.Fill(g, r, Theme.S(8), Enabled ? Theme.Mix(Theme.Raised, Color.FromArgb(34, 37, 46), Math.Max(hoverT, openT)) : Theme.Surface);
        Theme.Stroke(g, r, Theme.S(8), Theme.Mix(Theme.Border, Theme.Mix(Theme.Border, Theme.Accent, 0.55f), openT));
        using (var path = Theme.Round(r, Theme.S(8))) Reveal(g, path);
        FocusRing(g, r, Theme.S(8));
        Theme.DrawText(g, Text, Font, Enabled ? Theme.Text : Theme.Faint, new Rectangle(Theme.S(12), 0, Width - Theme.S(44), Height));
        // Chevron drawn as two strokes so it can flip smoothly while the list opens.
        float cx = Width - Theme.S(20), cy = Height / 2f, w = Theme.S(4.5f), h = Theme.S(2.5f) * (1 - 2 * openT);
        using (var p = new Pen(Enabled ? Theme.Mix(Theme.Muted, Theme.Text, openT) : Theme.Faint, Theme.S(1.4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(p, new[] { new PointF(cx - w, cy - h), new PointF(cx, cy + h), new PointF(cx + w, cy - h) });
    }
}

// The open list of a SelectBox: roomy rows, hover highlight, the chosen one in accent with a check.
public class SelectList : ScrollSurface {
    readonly List<object> items;
    readonly object selected;
    int hot;
    float reveal;
    public event Action<object> Picked;
    int RowH { get { return Theme.S(40); } }
    int Pad { get { return Theme.S(8); } }

    protected override bool Reveals { get { return true; } }

    readonly bool technical;
    public SelectList(List<object> items, object selected, int width, bool technical=false) {
        this.technical=technical;
        this.items = items;
        this.selected = selected;
        BackColor = Theme.Raised;
        Font = technical?Theme.Mono(10.5f):Theme.Body(10.5f);
        content = items.Count * RowH + Pad * 2;
        // At least as wide as the box, wider when a label and its detail need the room.
        int needed = 0;
        using (var bold = technical?Theme.Mono(10.5f,true):Theme.Body(10.5f,true)) using (var small = Theme.Mono(9.5f))
            foreach (var item in items) {
                var li = item as IListItem;
                int w = TextRenderer.MeasureText(li != null ? li.Label : item.ToString(), bold).Width;
                if (li != null && !String.IsNullOrEmpty(li.Detail)) w += TextRenderer.MeasureText(li.Detail, small).Width + Theme.S(12);
                needed = Math.Max(needed, w);
            }
        needed += Pad * 2 + Theme.S(18) + Theme.S(14) + (items.Count > 8 ? Theme.S(10) : 0);
        Size = new Size(Math.Max(width, needed), Math.Min(items.Count, 8) * RowH + Pad * 2);
        hot = items.IndexOf(selected);
    }

    public void Reveal(float t) { reveal = t; Invalidate(); }

    // Rows that matter: the chosen one carries a bar on its left edge and a faint fill of its own.
    void DrawRow(Graphics g, RectangleF row, bool chosen, bool hovered) {
        if (chosen && !hovered) Theme.Fill(g, row, Theme.S(8), Color.FromArgb(27, 30, 37));
        if (hovered) {
            Theme.Fill(g, row, Theme.S(8), Color.FromArgb(36, 39, 48));
            using (var shape = Theme.Round(row, Theme.S(8))) Reveal(g, shape, 34, 130);
        }
        if (chosen) Theme.Fill(g, new RectangleF(row.X + Theme.S(5), row.Y + (row.Height - Theme.S(16)) / 2, Theme.S(3), Theme.S(16)), Theme.S(1.5f), Theme.Accent);
    }

    int At(Point p) {
        int i = (int)Math.Floor((p.Y + offset - Pad) / RowH);
        return i >= 0 && i < items.Count ? i : -1;
    }

    protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { hot = Math.Max(0, Math.Min(items.Count - 1, hot + (e.KeyCode == Keys.Up ? -1 : 1))); Invalidate(); }
        if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) && hot >= 0 && Picked != null) Picked(items[hot]);
        base.OnKeyDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        int i = At(e.Location);
        if (i != hot && i >= 0) { hot = i; Invalidate(); }
        Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseClick(MouseEventArgs e) {
        int i = At(e.Location);
        if (i >= 0 && Picked != null) Picked(items[i]);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.Smooth(g);
        int slide = (int)((1 - reveal) * Theme.S(8)), right = Width - Pad - (content > Height ? Theme.S(10) : 0);
        using (var bold = technical?Theme.Mono(10.5f,true):Theme.Body(10.5f,true)) using (var small = Theme.Mono(9.5f))
            for (int i = 0; i < items.Count; i++) {
                int y = Pad + i * RowH - (int)offset - slide;
                if (y + RowH < 0 || y > Height) continue;
                var r = new Rectangle(Pad, y, right - Pad, RowH);
                bool chosen = Equals(items[i], selected);
                DrawRow(g, new RectangleF(r.X, r.Y + Theme.S(1), r.Width, r.Height - Theme.S(2)), chosen, i == hot);
                var li = items[i] as IListItem;
                string label = li != null ? li.Label : items[i].ToString(), detail = li != null ? li.Detail : null;
                int textRight = r.Right - Theme.S(14);
                if (!String.IsNullOrEmpty(detail)) {
                    int dw = TextRenderer.MeasureText(detail, small, Size.Empty, TextFormatFlags.NoPadding).Width;
                    Theme.DrawText(g, detail, small, Theme.Muted, new Rectangle(textRight - dw, r.Y, dw + Theme.S(2), r.Height));
                    textRight -= dw + Theme.S(12);
                }
                Theme.DrawText(g, label, chosen ? bold : Font, Theme.Text, new Rectangle(r.X + Theme.S(18), r.Y, textRight - r.X - Theme.S(18), r.Height));
            }
        PaintScrollbar(g);
    }
}

public class SearchBox : Painted {
    public readonly TextBox Input;
    public string Placeholder = "Buscar jogo", Glyph = Theme.IconSearch;
    float focusT;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
    public SearchBox() {
        Size = new Size(Theme.S(240), Theme.S(36));
        Input = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Raised, ForeColor = Theme.Text, Font = Theme.Body(10) };
        Controls.Add(Input);
        Input.GotFocus += delegate { Motion.To(Tuple.Create(this, "focus"), focusT, 1, Motion.Fast, v => { focusT = v; Invalidate(); }); };
        Input.LostFocus += delegate { Motion.To(Tuple.Create(this, "focus"), focusT, 0, Motion.Fast, v => { focusT = v; Invalidate(); }); };
        // Native cue banner: the placeholder is drawn by the text box itself.
        Input.HandleCreated += delegate { SendMessage(Input.Handle, 0x1501, new IntPtr(1), Placeholder); };
        Cursor = Cursors.IBeam;
        Click += delegate { Input.Focus(); };
    }
    protected override void OnLayout(LayoutEventArgs e) {
        if (Input != null) Input.SetBounds(Theme.S(34), (Height - Input.PreferredHeight) / 2, Width - Theme.S(44), Input.PreferredHeight);
        base.OnLayout(e);
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(ParentColor);
        Theme.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        Theme.Fill(g, r, Theme.S(8), Theme.Raised);
        Theme.Stroke(g, r, Theme.S(8), Theme.Mix(Theme.Border, Theme.Accent, focusT));
        using (var f = Theme.Glyphs(9.5f)) Theme.DrawGlyph(g, Glyph, f, Theme.Muted, new Rectangle(Theme.S(12), 0, Theme.S(20), Height));
    }
}

// Filter tabs with an optional icon and count; the selection pill slides between tabs.
public class Segments : Painted {
    public class Tab { public string Label; public int Count; public Image Icon; public object Tag; }
    public List<Tab> Tabs = new List<Tab>();
    public int Index;
    public event EventHandler Changed;
    int hot = -1;
    float pillX = -1, pillW;
    public Segments() { Height = Theme.S(36); Font = Theme.Body(10); Cursor = Cursors.Hand; }
    protected override bool Reveals { get { return true; } }

    List<Rectangle> TabBounds() {
        var list = new List<Rectangle>();
        int x = Theme.S(3);
        using (var bold = Theme.Body(10, true)) using(var countFont=Theme.Mono(9.5f))
            foreach (var t in Tabs) {
                int w = TextRenderer.MeasureText(t.Label, bold).Width + (t.Count >= 0 ? TextRenderer.MeasureText(t.Count.ToString(), countFont).Width : 0) + Theme.S(18) + (t.Icon != null ? Theme.S(22) : 0);
                list.Add(new Rectangle(x, Theme.S(3), w, Height - Theme.S(6)));
                x += w + Theme.S(2);
            }
        return list;
    }

    public int PreferredWidth() { var b = TabBounds(); return b.Count == 0 ? 0 : b.Last().Right + Theme.S(3); }

    public void Select(int index, bool animate) {
        Index = Math.Max(0, Math.Min(index, Tabs.Count - 1));
        var b = TabBounds();
        if (b.Count == 0) return;
        var target = b[Index];
        if (!animate || pillX < 0) { pillX = target.X; pillW = target.Width; Invalidate(); return; }
        float fx = pillX, fw = pillW;
        Motion.Run(Tuple.Create(this, "pill"), Motion.Normal, e => { pillX = fx + (target.X - fx) * e; pillW = fw + (target.Width - fw) * e; Invalidate(); });
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        int h = TabBounds().FindIndex(b => b.Contains(e.Location));
        if (h != hot) { hot = h; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) {
        int i = TabBounds().FindIndex(b => b.Contains(e.Location));
        if (i >= 0 && i != Index) { Select(i, true); if (Changed != null) Changed(this, EventArgs.Empty); }
        base.OnMouseClick(e);
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(ParentColor);
        Theme.Smooth(g);
        Theme.Fill(g, new RectangleF(0.5f, 0.5f, PreferredWidth() - 1, Height - 1), Theme.S(10), Theme.Surface);
        var bounds = TabBounds();
        if (pillX >= 0) Theme.Fill(g, new RectangleF(pillX, Theme.S(3), pillW, Height - Theme.S(6)), Theme.S(8), Theme.Raised);
        // Reveal: the tab under the pointer glows where the pointer is, and the strip's edge catches the light.
        if (hot >= 0 && hot < bounds.Count) using (var tab = Theme.Round(new RectangleF(bounds[hot].X, Theme.S(3), bounds[hot].Width, Height - Theme.S(6)), Theme.S(8))) Reveal(g, tab, 36, 0);
        using (var strip = Theme.Round(new RectangleF(0.5f, 0.5f, PreferredWidth() - 1, Height - 1), Theme.S(10))) Reveal(g, strip, 0, 120);
        using (var bold = Theme.Body(10, true)) using(var countFont=Theme.Mono(9.5f))
            for (int i = 0; i < bounds.Count; i++) {
                var b = bounds[i];
                var t = Tabs[i];
                bool sel = i == Index;
                int x = b.X + Theme.S(9);
                if (t.Icon != null) {
                    int s = Theme.S(16);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(t.Icon, x, b.Y + (b.Height - s) / 2, s, s);
                    x += Theme.S(22);
                }
                var f = sel ? bold : Font;
                int labelW = TextRenderer.MeasureText(t.Label, f, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, t.Label, f, new Rectangle(x, b.Y, labelW + 2, b.Height), sel || i == hot ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                if (t.Count >= 0) TextRenderer.DrawText(g, t.Count.ToString(), countFont, new Rectangle(x + labelW + Theme.S(6), b.Y, Theme.S(40), b.Height), Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
    }
}

// Scrollable surface with eased wheel scrolling and a slim draggable thumb.
public abstract class ScrollSurface : Painted {
    protected float offset;
    protected int content;
    float target;
    bool dragging;
    int dragStart;
    float dragOffset;
    public ScrollSurface() { TabStop = true; }
    protected int Max { get { return Math.Max(0, content - Height); } }
    protected void Clamp() { target = Math.Max(0, Math.Min(target, Max)); offset = Math.Max(0, Math.Min(offset, Max)); }
    protected void ScrollTop() { Motion.Stop(Tuple.Create(this, "scroll")); target = offset = 0; }
    protected override void OnMouseWheel(MouseEventArgs e) {
        target = Math.Max(0, Math.Min(target - e.Delta / 120f * Theme.S(100), Max));
        Motion.Follow(Tuple.Create(this, "scroll"), offset, () => target, v => { offset = v; Invalidate(); });
        base.OnMouseWheel(e);
    }
    Rectangle Thumb() {
        if (Max == 0) return Rectangle.Empty;
        int h = Math.Max(Theme.S(40), Height * Height / content);
        int y = (int)((Height - h) * (offset / Max));
        return new Rectangle(Width - Theme.S(8), y, Theme.S(5), h);
    }
    protected void PaintScrollbar(Graphics g) {
        var t = Thumb();
        if (t.IsEmpty) return;
        Theme.Fill(g, t, Theme.S(3), dragging ? Color.FromArgb(70, 74, 86) : Theme.Mix(Color.FromArgb(36, 39, 47), Color.FromArgb(62, 66, 78), hoverT));
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        if (!Thumb().IsEmpty && e.X >= Width - Theme.S(12)) { Motion.Stop(Tuple.Create(this, "scroll")); dragging = true; dragStart = e.Y; dragOffset = offset; return; }
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        if (dragging) {
            offset = target = Math.Max(0, Math.Min(Max, dragOffset + (e.Y - dragStart) * (Max / (float)Math.Max(1, Height - Thumb().Height))));
            Invalidate();
            return;
        }
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }
}

// Same glide and slim thumb as ScrollSurface, for a column of real controls (switches, lists).
public class SmoothScroll : Panel {
    public readonly Panel Content = new Panel();
    float offset, target;
    bool dragging;
    int dragStart;
    float dragOffset;

    public SmoothScroll() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Controls.Add(Content);
    }

    // WS_EX_COMPOSITED: the rows move as one image instead of repainting one by one while gliding.
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; } }

    int Max { get { return Math.Max(0, Content.Height - Height); } }
    object Key { get { return Tuple.Create(this, "scroll"); } }

    public void Refit() {
        target = Math.Max(0, Math.Min(target, Max));
        offset = Math.Max(0, Math.Min(offset, Max));
        Place();
    }

    void Place() {
        Content.Top = -(int)Math.Round(offset);
        Invalidate(new Rectangle(Width - Theme.S(12), 0, Theme.S(12), Height));
    }

    // Wheel messages over any row bubble up here, since none of the rows scroll by themselves.
    protected override void OnMouseWheel(MouseEventArgs e) {
        target = Math.Max(0, Math.Min(target - e.Delta / 120f * Theme.S(100), Max));
        Motion.Follow(Key, offset, () => target, v => { offset = v; Place(); });
        var handled = e as HandledMouseEventArgs;
        if (handled != null) handled.Handled = true;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Refit(); }

    Rectangle Thumb() {
        if (Max == 0) return Rectangle.Empty;
        int h = Math.Max(Theme.S(40), Height * Height / Content.Height);
        int y = (int)((Height - h) * (offset / Max));
        return new Rectangle(Width - Theme.S(8), y, Theme.S(5), h);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        var t = Thumb();
        if (t.IsEmpty) return;
        Theme.Smooth(g);
        Theme.Fill(g, t, Theme.S(3), dragging ? Color.FromArgb(70, 74, 86) : Color.FromArgb(48, 52, 62));
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        if (!Thumb().IsEmpty && e.X >= Width - Theme.S(12)) { Motion.Stop(Key); dragging = true; dragStart = e.Y; dragOffset = offset; }
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        if (dragging) {
            offset = target = Math.Max(0, Math.Min(Max, dragOffset + (e.Y - dragStart) * (Max / (float)Math.Max(1, Height - Thumb().Height))));
            Place();
        }
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Invalidate(); base.OnMouseUp(e); }
}

public class GameGrid : ScrollSurface {
    List<LibraryGame> games = new List<LibraryGame>();
    public Func<LibraryGame, bool> IsRunning = g => false;
    public string EmptyText = "";
    public event Action<LibraryGame, Rectangle> MenuRequested; // card bounds, in grid coordinates
    public event Action<LibraryGame, Point> PlayRequested;
    readonly Dictionary<string, float> cardHover = new Dictionary<string, float>();
    string hotKey;
    Form hoverOwner;
    bool playHot;
    int lastClick = -1, lastClickAt;
    // Reveal: where the pointer is (null when outside), and the light ring spreading from the last press.
    Point? pointer;
    string pressKey;
    Point pressAt;
    float ringT, pressDepth;
    Bitmap edgeLightImage, glossLightImage, playImage, activePlayImage;
    readonly Dictionary<string, Bitmap> hoverTiles = new Dictionary<string, Bitmap>();
    readonly Queue<string> hoverTileOrder = new Queue<string>();
    int RevealRadius { get { return Theme.S(170); } }
    readonly System.Diagnostics.Stopwatch appearClock = new System.Diagnostics.Stopwatch();
    int animatedCards, appearGeneration;
    double staggerMs;
    const double CardAppearMs = 260;

    public static Size Cover { get { return new Size(Theme.S(148), Theme.S(222)); } }
    int Gap { get { return Theme.S(18); } }
    int CardH { get { return Cover.Height + Theme.S(54); } }
    // Room around the grid for the cover's outer frame.
    int Edge { get { return Theme.S(12); } }
    int Columns { get { return Math.Max(1, (Width - Theme.S(10) - Edge + Gap) / (Cover.Width + Gap)); } }

    // One shared ticker; cards enter in reading order without delaying artwork loading.
    public void SetGames(List<LibraryGame> list, bool animate) {
        ResetHover();
        games = list;
        Relayout();
        int generation = ++appearGeneration;
        animatedCards = 0;
        if (!animate || games.Count == 0) { Invalidate(); return; }
        ScrollTop();
        int visibleRows = Math.Max(1, (Height + CardH + Gap - 1) / (CardH + Gap));
        animatedCards = Math.Min(games.Count, Columns * visibleRows);
        staggerMs = Math.Min(75, 900.0 / Math.Max(1, animatedCards - 1));
        appearClock.Restart();
        Motion.Run(Tuple.Create(this, "appear"), CardAppearMs + (animatedCards - 1) * staggerMs,
            e => { if (!IsDisposed && generation == appearGeneration) Invalidate(); },
            () => { if (!IsDisposed && generation == appearGeneration) { animatedCards = 0; appearClock.Stop(); Invalidate(); } });
    }

    float CardAppearance(int index) {
        if (index >= animatedCards) return 1;
        float progress = (float)Math.Max(0, Math.Min(1, (appearClock.Elapsed.TotalMilliseconds - index * staggerMs) / CardAppearMs));
        return Motion.EaseOut(progress);
    }

    public void Relayout() {
        int rows = (games.Count + Columns - 1) / Columns;
        content = Edge + rows * (CardH + Gap);
        Clamp();
        Invalidate();
    }

    Rectangle CardRect(int i) {
        int cols = Columns;
        return new Rectangle(Edge + (i % cols) * (Cover.Width + Gap), Edge + (i / cols) * (CardH + Gap) - (int)offset, Cover.Width, CardH);
    }

    int HitTest(Point p) {
        for (int i = 0; i < games.Count; i++) {
            float appearance = CardAppearance(i);
            if (appearance < 0.6f) continue;
            var card = CardRect(i);
            card.Y += (int)((1 - appearance) * Theme.S(16));
            if (card.Contains(p)) return i;
        }
        return -1;
    }

    void SetHot(string key) {
        if (key == hotKey) return;
        foreach (var k in new[] { hotKey, key }) {
            if (k == null) continue;
            string id = k;
            float from;
            cardHover.TryGetValue(id, out from);
            if(id==key){from=Math.Max(from,.35f);cardHover[id]=from;InvalidateCard(id);}
            Motion.To(Tuple.Create(this, id), from, id == key ? 1 : 0, id==key?Motion.Fast:110, v => { cardHover[id] = v; InvalidateCard(id); });
        }
        hotKey = key;
    }

    // Only the card (with room for its lift and glow) is repainted while its hover fades.
    void InvalidateCard(string key) {
        int i = games.FindIndex(g => g.Key == key);
        if (i < 0) return;
        int m = Artwork.CardMargin + Theme.S(8);
        Invalidate(Rectangle.Inflate(CardRect(i), m, m));
        Motion.Repaint(this);
    }

    bool CanPlay(LibraryGame g) { return g.Installed && !IsRunning(g); }

    // Round play button in the middle of the cover, shown on hover.
    Rectangle PlayRect(int i) {
        var card = CardRect(i);
        int d = Theme.S(56);
        return new Rectangle(card.X + (Cover.Width - d) / 2, card.Y + (Cover.Height - d) / 2, d, d);
    }

    bool OnPlay(int i, Point p) {
        if (i < 0 || !CanPlay(games[i])) return false;
        var r = PlayRect(i);
        float dx = p.X - (r.X + r.Width / 2f), dy = p.Y - (r.Y + r.Height / 2f);
        return dx * dx + dy * dy <= r.Width * r.Width / 4f;
    }

    void Play(int i, Point at) { lastClick = -1; if (PlayRequested != null && CanPlay(games[i])) PlayRequested(games[i], at); }

    protected override void OnResize(EventArgs e) { Relayout(); base.OnResize(e); }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);hoverOwner=FindForm();if(hoverOwner!=null)hoverOwner.Deactivate+=OwnerDeactivated;}
    protected override void OnHandleDestroyed(EventArgs e){if(hoverOwner!=null)hoverOwner.Deactivate-=OwnerDeactivated;hoverOwner=null;base.OnHandleDestroyed(e);}
    void OwnerDeactivated(object sender,EventArgs e){ResetHover();}
    void ResetHover(){
        foreach(string key in cardHover.Keys.ToList())Motion.Stop(Tuple.Create(this,key));
        cardHover.Clear();hotKey=null;playHot=false;pointer=null;
        Invalidate();Motion.Repaint(this);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        int h = HitTest(e.Location);
        RevealCrossedCards(e.Location,h>=0?games[h].Key:null);
        SetHot(h >= 0 ? games[h].Key : null);
        bool overPlay = OnPlay(h, e.Location);
        if (overPlay != playHot) { playHot = overPlay; if (hotKey != null) InvalidateCard(hotKey); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        RevealAt(e.Location);
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { ResetHover(); base.OnMouseLeave(e); }
    protected override void OnMouseWheel(MouseEventArgs e){ResetHover();base.OnMouseWheel(e);}

    void RevealCrossedCards(Point at,string next){
        if(pointer==null)return;var previous=pointer.Value;
        if(Math.Abs(previous.X-at.X)+Math.Abs(previous.Y-at.Y)<Theme.S(32))return;
        for(int i=0;i<games.Count;i++){
            string key=games[i].Key;if(key==next||key==hotKey||CardAppearance(i)<.6f)continue;
            var rect=CardRect(i);if(!rect.IntersectsWith(ClientRectangle)||!Crosses(previous,at,rect))continue;
            float from;cardHover.TryGetValue(key,out from);from=Math.Max(from,.35f);cardHover[key]=from;InvalidateCard(key);
            string id=key;Motion.To(Tuple.Create(this,id),from,0,110,v=>{cardHover[id]=v;InvalidateCard(id);});
        }
    }
    static bool Crosses(Point a,Point b,Rectangle r){float first=0,last=1,dx=b.X-a.X,dy=b.Y-a.Y;return ClipLine(-dx,a.X-r.Left,ref first,ref last)&&ClipLine(dx,r.Right-a.X,ref first,ref last)&&ClipLine(-dy,a.Y-r.Top,ref first,ref last)&&ClipLine(dy,r.Bottom-a.Y,ref first,ref last);}
    static bool ClipLine(float p,float q,ref float first,ref float last){if(p==0)return q>=0;float t=q/p;if(p<0){if(t>last)return false;if(t>first)first=t;}else{if(t<first)return false;if(t<last)last=t;}return true;}

    // Repaints only the band the light covers, before and after the move.
    void RevealAt(Point? p) {
        if (p == pointer) return;
        foreach (var at in new[] { pointer, p })
            if (at != null) Invalidate(new Rectangle(at.Value.X - RevealRadius - 2, at.Value.Y - RevealRadius - 2, RevealRadius * 2 + 4, RevealRadius * 2 + 4));
        pointer = p;
        Motion.Repaint(this);
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        int i = HitTest(e.Location);
        if (i >= 0 && e.Button == MouseButtons.Left) {
            pressKey = games[i].Key;
            pressAt = e.Location;
            Motion.Run(Tuple.Create(this, "press"), 520, t => { ringT = t; Invalidate(); }, () => { pressKey = null; Invalidate(); });
            Motion.To(Tuple.Create(this, "depth"), pressDepth, 1, Motion.Fast, v => { pressDepth = v; Invalidate(); });
        }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) {
        Motion.To(Tuple.Create(this, "depth"), pressDepth, 0, Motion.Normal, v => { pressDepth = v; Invalidate(); });
        base.OnMouseUp(e);
    }
    protected override void OnMouseClick(MouseEventArgs e) {
        int i = HitTest(e.Location);
        if (i >= 0 && e.Button == MouseButtons.Left) {
            // The popover opens beside the card, so a second click still lands here and counts as a double click.
            bool second = i == lastClick && unchecked(Environment.TickCount - lastClickAt) <= SystemInformation.DoubleClickTime;
            if (OnPlay(i, e.Location) || second) Play(i, e.Location);
            else {
                lastClick = i;
                lastClickAt = Environment.TickCount;
                if (MenuRequested != null) MenuRequested(games[i], CardRect(i));
            }
        }
        base.OnMouseClick(e);
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e) {
        int i = HitTest(e.Location);
        if (i >= 0 && e.Button == MouseButtons.Left) Play(i, e.Location);
        base.OnMouseDoubleClick(e);
    }

    readonly Dictionary<string, Bitmap> labels = new Dictionary<string, Bitmap>();

    // Name plus launcher line, rendered once per game into a transparent bitmap.
    Bitmap Label(LibraryGame game) {
        string key = game.Key + "|" + game.Excluded + "|" + game.Competitive + "|" + Cover.Width;
        Bitmap bmp;
        if (labels.TryGetValue(key, out bmp)) return bmp;
        bmp = new Bitmap(Cover.Width, Theme.S(46), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp)) using (var nameFont = Theme.Body(10.5f, true)) using (var small = Theme.Body(9.5f))
        using (var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center }) {
            // No grid fitting: it snaps Geist's curves to whole pixels, which reads as pixelated at these sizes.
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using (var b = new SolidBrush(game.Excluded ? Theme.Muted : Theme.Text)) g.DrawString(game.Name, nameFont, b, new RectangleF(-Theme.S(1), 0, bmp.Width + Theme.S(1), Theme.S(22)), format);
            float lx = 0;
            var icon = game.Launcher.Icon;
            if (icon != null) { g.DrawImage(icon, 0, Theme.S(26), Theme.S(14), Theme.S(14)); lx = Theme.S(20); }
            using (var b = new SolidBrush(Theme.Muted)) g.DrawString(game.Excluded && game.Competitive ? "Competitivo" : game.Launcher.Name, small, b, new RectangleF(lx - Theme.S(1), Theme.S(24), bmp.Width - lx, Theme.S(18)), format);
        }
        if (labels.Count > 400) { foreach (var old in labels.Values) old.Dispose(); labels.Clear(); }
        labels[key] = bmp;
        return bmp;
    }

    static System.Drawing.Imaging.ImageAttributes Faded(float opacity) {
        var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity });
        return ia;
    }

    // Rasterize radial lights once, rather than evaluating a gradient for every mouse position.
    static Bitmap LightImage(int radius, int alpha) {
        var image = new Bitmap(radius * 2 + 2, radius * 2 + 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(image)) using (var light = Theme.Light(new Point(radius, radius), radius, alpha)) {
            g.Clear(Color.Transparent);
            g.FillRectangle(light, 0, 0, image.Width, image.Height);
        }
        return image;
    }
    TextureBrush CachedLight(bool gloss, Point at, float opacity) {
        int radius = gloss ? Theme.S(140) : RevealRadius;
        if (gloss && glossLightImage == null) glossLightImage = LightImage(radius, 46);
        if (!gloss && edgeLightImage == null) edgeLightImage = LightImage(radius, 210);
        var image = gloss ? glossLightImage : edgeLightImage;
        TextureBrush brush;
        if (opacity >= .99f) brush = new TextureBrush(image, WrapMode.Clamp);
        else using (var attributes = Faded(opacity)) brush = new TextureBrush(image, new Rectangle(0, 0, image.Width, image.Height), attributes);
        brush.WrapMode = WrapMode.Clamp;
        brush.TranslateTransform(at.X - radius, at.Y - radius);
        return brush;
    }
    void DrawGloss(Graphics g, GraphicsPath path, Point at, float opacity) {
        int radius = Theme.S(140);
        if (glossLightImage == null) glossLightImage = LightImage(radius, 46);
        var state = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var r = new Rectangle(at.X - radius, at.Y - radius, glossLightImage.Width, glossLightImage.Height);
        using (var attributes = Faded(opacity)) g.DrawImage(glossLightImage, r, 0, 0, glossLightImage.Width, glossLightImage.Height, GraphicsUnit.Pixel, attributes);
        g.Restore(state);
    }
    Bitmap PlayImage(bool active) {
        var image = active ? activePlayImage : playImage;
        if (image != null) return image;
        int d = Theme.S(56), pad = Theme.S(2);
        image = new Bitmap(d + pad * 2, d + pad * 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(image)) {
            Theme.Smooth(g);
            var circle = new RectangleF(pad, pad, d, d);
            using (var fill = new SolidBrush(active ? Theme.Accent : Color.FromArgb(170, 10, 11, 14))) g.FillEllipse(fill, circle);
            if (!active) using (var ring = new Pen(Color.FromArgb(110, 255, 255, 255), Theme.S(1.2f))) g.DrawEllipse(ring, circle);
            float cx = pad + d / 2f, cy = pad + d / 2f, s = d * .3f;
            var tri = new[] { new PointF(cx - s * .38f, cy - s * .5f), new PointF(cx + s * .55f, cy), new PointF(cx - s * .38f, cy + s * .5f) };
            var ink = active ? Theme.AccentInk : Color.White;
            using (var fill = new SolidBrush(ink)) g.FillPolygon(fill, tri);
            using (var pen = new Pen(ink, Theme.S(2.5f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, tri);
        }
        if (active) activePlayImage = image; else playImage = image;
        return image;
    }
    Bitmap HoverTile(LibraryGame game, Artwork.Art art, float opacity, bool active) {
        int level = Math.Max(0, Math.Min(24, (int)Math.Round(opacity * 24)));
        string key = game.Key + "|" + game.Name + "|" + game.Excluded + "|" + game.Competitive + "|" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(art) + "|" + level + "|" + active;
        Bitmap tile;
        if (hoverTiles.TryGetValue(key, out tile)) return tile;
        int margin = Artwork.CardMargin;
        tile = new Bitmap(Cover.Width + margin * 2, CardH + margin * 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        float hover = level / 24f;
        using (var g = Graphics.FromImage(tile)) {
            Theme.Smooth(g);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            var r = new Rectangle(margin, margin, Cover.Width, Cover.Height);
            Artwork.DrawCoverGlow(g,r,art.Tint,hover);
            g.DrawImageUnscaled(art.Cover, r.Location);
            using (var path = Theme.Round(r, Artwork.Radius)) {
                if (game.Excluded) using (var shade = new SolidBrush(Color.FromArgb(170, Theme.Base))) g.FillPath(shade, path);
                using (var pen = new Pen(Theme.Mix(Color.FromArgb(28, 31, 38), Color.FromArgb(66, 71, 84), hover))) g.DrawPath(pen, path);
            }
            if (hover > 0 && CanPlay(game)) {
                var play = PlayImage(active);int pad = Theme.S(2);
                var target = new Rectangle(margin + (Cover.Width - Theme.S(56)) / 2 - pad, margin + (Cover.Height - Theme.S(56)) / 2 - pad, play.Width, play.Height);
                using (var alpha = Faded(hover)) g.DrawImage(play, target, 0, 0, play.Width, play.Height, GraphicsUnit.Pixel, alpha);
            }
            g.DrawImageUnscaled(Label(game), margin, margin + Cover.Height + Theme.S(8));
        }
        while (hoverTiles.Count >= 192) { string oldest = hoverTileOrder.Dequeue(); hoverTiles[oldest].Dispose(); hoverTiles.Remove(oldest); }
        hoverTiles.Add(key, tile);hoverTileOrder.Enqueue(key);
        return tile;
    }

    static void Pill(Graphics g, string text, Font f, Color dot, int x, int y) {
        int w = TextRenderer.MeasureText(text, f, Size.Empty, TextFormatFlags.NoPadding).Width + Theme.S(26);
        Theme.Fill(g, new RectangleF(x, y, w, Theme.S(24)), Theme.S(12), Color.FromArgb(225, 10, 11, 14));
        using (var b = new SolidBrush(dot)) g.FillEllipse(b, x + Theme.S(9), y + Theme.S(9), Theme.S(6), Theme.S(6));
        TextRenderer.DrawText(g, text, f, new Rectangle(x + Theme.S(19), y, w, Theme.S(24)), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        Theme.Smooth(g);
        if (games.Count == 0) {
            using (var f = Theme.Body(10.5f)) Theme.DrawText(g, EmptyText, f, Theme.Muted, new Rectangle(0, 0, Width, Theme.S(120)), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        // One light for every lit edge in this frame (the pointer is the same for all of them).
        var edgeLight = pointer != null ? CachedLight(false, pointer.Value, 1) : null;
        var edgePen = edgeLight != null ? new Pen(edgeLight, Theme.S(1.6f)) : null;
        using (edgeLight) using (edgePen)
        using (var nameFont = Theme.Body(10.5f, true)) using (var small = Theme.Body(9.5f))
            for (int i = 0; i < games.Count; i++) {
                float appearance = CardAppearance(i);
                if (appearance <= 0) continue;
                var card = CardRect(i);
                card.Y += (int)((1 - appearance) * Theme.S(16));
                if (card.Bottom < 0 || card.Top > Height) continue;
                // Skip cards outside the area being repainted (a hover fade touches one card, not the grid).
                if (!e.ClipRectangle.IntersectsWith(Rectangle.Inflate(card, Artwork.CardMargin + Theme.S(8), Artwork.CardMargin + Theme.S(8)))) continue;
                var game = games[i];
                float hov;
                cardHover.TryGetValue(game.Key, out hov);
                var art = Artwork.Get(game.Key);
                bool pressed = pressKey == game.Key;
                // Reuse the static part of the hover; only pointer-following light is drawn live.
                if (!pressed && appearance >= 1 && art != null && art.Cover != null &&
                    DateTime.UtcNow.Ticks - art.LoadedTicks >= TimeSpan.TicksPerMillisecond * 320 && game.Installed && !IsRunning(game)) {
                    var tile = HoverTile(game, art, hov, playHot && game.Key == hotKey);
                    int margin = Artwork.CardMargin;
                    var mode = g.InterpolationMode;var pixelMode = g.PixelOffsetMode;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImageUnscaled(tile, card.X - margin, card.Y - margin);
                    g.InterpolationMode = mode;g.PixelOffsetMode = pixelMode;
                    var cover = new Rectangle(card.X, card.Y, Cover.Width, Cover.Height);
                    using (var outline = Theme.Round(cover, Artwork.Radius)) {
                        if (edgePen != null && Theme.Distance(pointer.Value, cover) < RevealRadius) g.DrawPath(edgePen, outline);
                    }
                    continue;
                }
                // The pressed card sinks back and shrinks slightly. Hover has no lift: a rise moves in whole-pixel
                // steps (it read as stutter) and a sub-pixel one forces resampling the cover every frame (2× the cost).
                float depth = pressed ? pressDepth : 0;
                const bool fractional = false;
                var beforeLift = g.Save();
                var coverRect = Rectangle.Inflate(new Rectangle(card.X, card.Y, Cover.Width, Cover.Height), -(int)(Theme.S(4) * depth), -(int)(Theme.S(6) * depth));

                if(art!=null)Artwork.DrawCoverGlow(g,coverRect,art.Tint,hov);

                using (var path = Theme.Round(coverRect, Artwork.Radius)) {
                    // Art fades in when it finishes loading instead of popping in.
                    float fade = art == null ? 0 : (float)Math.Min(1, (DateTime.UtcNow.Ticks - art.LoadedTicks) / (TimeSpan.TicksPerMillisecond * 320.0));
                    if (art != null && fade < 1) Motion.Run(Tuple.Create(this, "artfade"), 120, _ => Invalidate());
                    if (art != null && art.Cover != null) {
                        if (fade < 1) using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
                        // Unscaled covers are copied pixel for pixel (fast path); only the pressed card is resampled.
                        bool exact = coverRect.Size == art.Cover.Size && !fractional;
                        var interpolation = g.InterpolationMode;
                        var offsetMode = g.PixelOffsetMode;
                        g.InterpolationMode = exact ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        if (fade >= 1) g.DrawImage(art.Cover, coverRect);
                        else using (var ia = Faded(Motion.EaseOut(fade))) g.DrawImage(art.Cover, coverRect, 0, 0, art.Cover.Width, art.Cover.Height, GraphicsUnit.Pixel, ia);
                        g.InterpolationMode = interpolation;
                        g.PixelOffsetMode = offsetMode;
                    } else {
                        using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
                        // No cover: the game's own icon on a plain tile, with its name for recognition.
                        int s = Theme.S(56);
                        if (art != null && art.Icon != null) g.DrawImage(art.Icon, coverRect.X + (coverRect.Width - s) / 2, coverRect.Y + Theme.S(64), s, s);
                        Theme.DrawText(g, game.Name, nameFont, Theme.Muted, new Rectangle(coverRect.X + Theme.S(12), coverRect.Y + Theme.S(134), coverRect.Width - Theme.S(24), Theme.S(60)), TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
                    }
                    if (game.Excluded) using (var shade = new SolidBrush(Color.FromArgb(170, Theme.Base))) g.FillPath(shade, path);
                    // Hover changes the backdrop only. The press ring remains local to a click.
                    if (pressed && ringT < 1) {
                        float r = Theme.S(30) + Cover.Height * 1.1f * Motion.EaseOut(ringT);
                        using (var ring = Theme.Light(pressAt, r, (int)(90 * (1 - ringT)))) g.FillPath(ring, path);
                    }
                    using (var p = new Pen(Theme.Mix(Color.FromArgb(28, 31, 38), Color.FromArgb(66, 71, 84), hov))) g.DrawPath(p, path);
                    // Reveal border: edges near the pointer catch the light, on this card and its neighbours.
                    if (edgePen != null && Theme.Distance(pointer.Value, coverRect) < RevealRadius) g.DrawPath(edgePen, path);
                }
                if (hov > 0 && CanPlay(game)) {
                    var pr = PlayRect(i);
                    pr.Y += (int)((1 - appearance) * Theme.S(16));
                    // Reuse the play disc; fading a small sprite avoids rebuilding its paths every frame.
                    bool on = playHot && game.Key == hotKey;
                    var image = PlayImage(on);
                    int pad = Theme.S(2);
                    var target = new Rectangle(pr.X - pad, pr.Y - pad, image.Width, image.Height);
                    var interpolation = g.InterpolationMode;
                    var offsetMode = g.PixelOffsetMode;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    using (var attributes = Faded(hov)) g.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    g.InterpolationMode = interpolation;
                    g.PixelOffsetMode = offsetMode;
                }
                if (IsRunning(game)) Pill(g, "Em jogo", small, Theme.Live, coverRect.X + Theme.S(8), coverRect.Y + Theme.S(8));
                else if (!game.Installed) Pill(g, "Não encontrado", small, Theme.Warn, coverRect.X + Theme.S(8), coverRect.Y + Theme.S(8));

                // Name and launcher come from a cached transparent bitmap: drawing text costs ~0.6 ms a line,
                // which over every card was most of the frame time while hovering.
                var labelImage = Label(game);
                g.InterpolationMode = fractional ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(labelImage, card.X, coverRect.Bottom + Theme.S(8), labelImage.Width, labelImage.Height);
                g.Restore(beforeLift);
                if (appearance < 1) using (var veil = new SolidBrush(Color.FromArgb((int)((1 - appearance) * 255), BackColor)))
                    g.FillRectangle(veil, card);
            }
        PaintScrollbar(g);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (edgeLightImage != null) edgeLightImage.Dispose();
            if (glossLightImage != null) glossLightImage.Dispose();
            if (playImage != null) playImage.Dispose();
            if (activePlayImage != null) activePlayImage.Dispose();
            foreach (var image in hoverTiles.Values) image.Dispose();
            hoverTiles.Clear();hoverTileOrder.Clear();
            foreach (var image in labels.Values) image.Dispose();
            labels.Clear();
        }
        base.Dispose(disposing);
    }
}
