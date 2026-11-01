using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

public static class Theme {
    public static readonly Color Base = Color.FromArgb(6, 7, 9);
    public static readonly Color Sidebar = Color.FromArgb(10, 11, 14);
    public static readonly Color Surface = Color.FromArgb(17, 19, 24);
    public static readonly Color Raised = Color.FromArgb(24, 26, 32);
    public static readonly Color Border = Color.FromArgb(34, 37, 45);
    public static readonly Color Text = Color.FromArgb(246, 247, 249);
    public static readonly Color Muted = Color.FromArgb(184, 189, 199);
    public static readonly Color Faint = Color.FromArgb(128, 133, 146);
    public static readonly Color Danger = Color.FromArgb(242, 110, 110);
    // Platinum: primary actions are near-white with dark ink, matching the white half of the logo.
    public static readonly Color Accent = Color.FromArgb(236, 238, 242);
    public static readonly Color AccentInk = Color.FromArgb(10, 11, 14);
    // The logo's cyan, kept for small brand touches (history tags), never for buttons.
    public static readonly Color Brand = Color.FromArgb(76, 196, 255);
    public static readonly Color Live = Color.FromArgb(91, 214, 138);
    public static readonly Color Warn = Color.FromArgb(242, 184, 75);

    static float scale = 0;
    public static float Scale {
        get {
            if (scale == 0) using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            return scale;
        }
    }
    public static int S(float px) { return (int)Math.Round(px * Scale); }

    // Geist is embedded locally; Windows' symbol font is retained only for pictograms.
    static string icons;
    static bool Installed(string name) {
        using (var installed = new InstalledFontCollection())
            return installed.Families.Any(f => String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
    }
    static string IconFamily { get { return icons ?? (icons = Installed("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets"); } }

    public static Font Title(float size) { return LocalFonts.Create(size,false,true,true); }
    public static Font Body(float size, bool semibold = false) {
        return LocalFonts.Create(size,false,semibold);
    }
    public static Font Mono(float size,bool emphasis=false) { return LocalFonts.Create(size,true,emphasis); }
    public static Font Glyphs(float size) { return new Font(IconFamily, size); }

    // Segoe Fluent Icons code points.
    public const string IconLibrary = "", IconSettings = "", IconActivity = "", IconAdd = "",
        IconRefresh = "", IconSearch = "", IconPause = "", IconPlay = "", IconChevron = "",
        IconFolder = "", IconCheck = "", IconClose = "";

    // Reads the closest PNG entry from an .ico file; Icon.ToBitmap() garbles PNG-compressed entries.
    public static Image IconImage(string icoFile, int size) {
        byte[] ico = System.IO.File.ReadAllBytes(icoFile);
        int count = BitConverter.ToUInt16(ico, 4), best = 0, bestDiff = int.MaxValue;
        for (int i = 0; i < count; i++) {
            int dim = ico[6 + i * 16] == 0 ? 256 : ico[6 + i * 16];
            int diff = dim >= size ? dim - size : (size - dim) * 4;
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        int length = BitConverter.ToInt32(ico, 6 + best * 16 + 8), offset = BitConverter.ToInt32(ico, 6 + best * 16 + 12);
        using (var stream = new System.IO.MemoryStream(ico, offset, length)) using (var image = Image.FromStream(stream)) return new Bitmap(image);
    }

    public static GraphicsPath Round(RectangleF r, float radius) {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, RectangleF r, float radius, Color color) {
        using (var path = Round(r, radius)) using (var b = new SolidBrush(color)) g.FillPath(b, path);
    }

    public static void Stroke(Graphics g, RectangleF r, float radius, Color color) {
        using (var path = Round(r, radius)) using (var p = new Pen(color)) g.DrawPath(p, path);
    }

    public static void Smooth(Graphics g) {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public static void DrawText(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter) {
        TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    public static void DrawGlyph(Graphics g, string glyph, Font f, Color c, Rectangle r) {
        TextRenderer.DrawText(g, glyph, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoClipping | TextFormatFlags.NoPadding);
    }

    // Fluent "Reveal" light: white at the pointer, fading to nothing at the radius. Used as a fill for the
    // gloss on the hovered item and as a pen for the borders it reaches, so nearby edges catch the light too.
    public static PathGradientBrush Light(PointF center, float radius, int alpha) {
        using (var circle = new GraphicsPath()) {
            circle.AddEllipse(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            var b = new PathGradientBrush(circle) { CenterPoint = center, CenterColor = Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), 255, 255, 255), SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) } };
            b.SetSigmaBellShape(1f, 1f);
            return b;
        }
    }

    // Distance from a point to the nearest edge of a rectangle (0 inside).
    public static float Distance(Point p, Rectangle r) {
        int dx = Math.Max(0, Math.Max(r.Left - p.X, p.X - r.Right)), dy = Math.Max(0, Math.Max(r.Top - p.Y, p.Y - r.Bottom));
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public static Color FromHsl(float h, float s, float l) {
        float c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h / 60f % 2 - 1)), m = l - c / 2;
        float r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    public static Color Mix(Color a, Color b, float t) {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    public static ContextMenuStrip Menu() {
        var menu = new ContextMenuStrip { Renderer = new MenuRenderer(), BackColor = Raised, ForeColor = Text, Font = Body(10), ShowImageMargin = false, Padding = new Padding(S(4)) };
        menu.MouseMove += delegate { menu.Invalidate(); };
        menu.HandleCreated += delegate { int round = 2, border = Border.R | Border.G << 8 | Border.B << 16; if (DwmSetWindowAttribute(menu.Handle, 33, ref round, 4) == 0) { DwmSetWindowAttribute(menu.Handle, 34, ref border, 4); menu.Tag = "rounded"; } };
        // Every menu fades in.
        menu.Opening += delegate {
            menu.Opacity = 0;
            Motion.Run(Tuple.Create(menu, "fade"), Motion.Fast, e => { if (!menu.IsDisposed) menu.Opacity = e; });
        };
        return menu;
    }

    // Shows a one-off menu and disposes it once closed.
    public static void ShowMenu(ContextMenuStrip menu, Control owner, Point at) {
        menu.Closed += delegate { owner.BeginInvoke(new Action(menu.Dispose)); };
        menu.Show(owner, at);
    }

    // Floating panel around a custom control (dropdown lists, help cards); fades in, disposes itself once closed.
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    // Windows 11 rounds the popup window itself and draws its hairline border, so the content needs no
    // frame of its own. Older Windows refuses the attribute and keeps the square framed popup.
    public static ToolStripDropDown RoundedDrop() {
        var drop = new ToolStripDropDown { Padding = Padding.Empty, BackColor = Raised, DropShadowEnabled = false };
        drop.HandleCreated += delegate {
            int round = 2, border = Border.R | Border.G << 8 | Border.B << 16;
            if (DwmSetWindowAttribute(drop.Handle, 33, ref round, 4) != 0) { drop.Padding = new Padding(1); return; }
            DwmSetWindowAttribute(drop.Handle, 34, ref border, 4);
        };
        return drop;
    }

    public static ToolStripDropDown Popover(Control content, Action<float> reveal = null) {
        var drop = RoundedDrop();
        drop.Items.Add(new ToolStripControlHost(content) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = content.Size });
        drop.Opening += delegate {
            drop.Opacity = 0;
            Motion.Run(Tuple.Create(drop, "fade"), Motion.Normal, t => {
                if (drop.IsDisposed) return;
                drop.Opacity = t;
                if (reveal != null) reveal(t);
            });
        };
        drop.Closed += delegate { if (drop.IsHandleCreated) drop.BeginInvoke(new Action(drop.Dispose)); };
        return drop;
    }

    // Below the anchor (above it when the screen ends first), never past the app window. alignRight keeps a
    // wider popup flush with the anchor's right edge, for controls that sit on the right of a card.
    public static void ShowNear(ToolStripDropDown drop, Control anchor, int gap, bool alignRight = false) {
        var work = Screen.FromControl(anchor).WorkingArea;
        var form = anchor.FindForm();
        var bounds = form != null ? Rectangle.Intersect(work, form.RectangleToScreen(form.ClientRectangle)) : work;
        var box = anchor.RectangleToScreen(anchor.ClientRectangle);
        var size = drop.GetPreferredSize(Size.Empty);
        int x = alignRight || box.Left + size.Width > bounds.Right - gap ? box.Right - size.Width : box.Left;
        x = Math.Max(bounds.Left + gap, Math.Min(x, bounds.Right - gap - size.Width));
        int y = box.Bottom + gap;
        if (y + size.Height > work.Bottom) y = box.Top - gap - size.Height;
        drop.Show(new Point(x, y));
    }

    // Dark, rounded context menus (tray and in-app).
    class MenuRenderer : ToolStripProfessionalRenderer {
        public MenuRenderer() : base(new Colors()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) {
            e.Graphics.Clear(Raised);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
            // A rounded window already has its border from Windows; a square line would poke out of the corners.
            if (!Equals(e.ToolStrip.Tag, "rounded")) using (var p = new Pen(Border)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Smooth(e.Graphics);
            var item = new RectangleF(S(2), 0, e.Item.Width - S(4), e.Item.Height);
            Fill(e.Graphics, item, S(5), Color.FromArgb(36, 39, 48));
            // Reveal on menu items: light at the pointer, measured from the item's own corner.
            var p = e.ToolStrip.PointToClient(Control.MousePosition);
            var at = new PointF(p.X - e.Item.Bounds.X, p.Y - e.Item.Bounds.Y);
            using (var shape = Round(item, S(5))) {
                using (var light = Light(at, e.Item.Width * 0.8f, 34)) e.Graphics.FillPath(light, shape);
                using (var light = Light(at, e.Item.Width * 0.8f, 130)) using (var pen = new Pen(light, S(1.2f))) e.Graphics.DrawPath(pen, shape);
            }
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
            e.TextColor = e.Item.Enabled ? Text : Muted;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {
            int y = e.Item.Height / 2;
            using (var p = new Pen(Border)) e.Graphics.DrawLine(p, S(8), y, e.Item.Width - S(8), y);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }
    }

    class Colors : ProfessionalColorTable {
        public override Color ToolStripDropDownBackground { get { return Raised; } }
        public override Color MenuBorder { get { return Border; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color ImageMarginGradientBegin { get { return Raised; } }
        public override Color ImageMarginGradientMiddle { get { return Raised; } }
        public override Color ImageMarginGradientEnd { get { return Raised; } }
    }
}
