using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

// Card floating over the dimmed window. Subclasses add controls and place them in LayoutCard.
//
// Opening and closing animate a picture of the finished card (fade, a small rise and scale) instead of
// moving live controls: the controls are laid out once at rest, photographed, hidden while the picture
// plays, and shown again at the end. One image draw per frame is what keeps it smooth at high refresh rates;
// relaying out and repainting a few dozen controls every frame was not.
public abstract class Modal : Painted {
    readonly Func<Bitmap> snapshot;
    Bitmap backdrop, cardImage;
    protected float t;
    bool closing, capturing, live;
    protected Size MaxCard = new Size(Theme.S(780), Theme.S(660));

    public event EventHandler Closed;

    protected Modal(Func<Bitmap> windowSnapshot) {
        snapshot = windowSnapshot;
        BackColor = Theme.Surface; // children paint on the card color
    }

    protected Label CardLabel(string text, Font font, Color color) {
        return new Label { Text = text, Font = font, ForeColor = color, BackColor = Theme.Surface, AutoSize = false };
    }

    // The card at rest; the animation never moves it, only the picture of it.
    protected Rectangle Card {
        get {
            int w = Math.Min(MaxCard.Width, Width - Theme.S(64)), h = Math.Min(MaxCard.Height, Height - Theme.S(56));
            return new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
        }
    }

    void SetChildren(bool visible) { foreach (Control c in Controls) c.Visible = visible; }

    // The window behind the modal, in the premultiplied format GDI+ can copy without converting: about 20× cheaper per frame.
    Bitmap TakeBackdrop() {
        using (var shot = snapshot()) {
            var fast = new Bitmap(shot.Width, shot.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(fast)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(shot, 0, 0); }
            return fast;
        }
    }

    public void Open() {
        backdrop = TakeBackdrop();
        SetChildren(true);
        PerformLayout();
        cardImage = PhotographCard();
        if (cardImage == null) { t = 1; live = true; OnShown(); Invalidate(); return; }
        SetChildren(false);
        t = 0;
        Motion.Run(Tuple.Create(this, "show"), Motion.Normal + 80, e => { t = e; Invalidate(); }, delegate {
            if (closing) return;
            t = 1;
            live = true;
            SetChildren(true);
            OnShown();
            Invalidate();
        });
    }

    protected virtual void OnShown() { }
    protected virtual void OnClosing() { }

    public void Close() {
        if (closing) return;
        closing = true;
        OnClosing();
        if (live) {
            // Photograph the card as it is now (switches and lists may have changed since opening).
            var fresh = PhotographCard();
            if (fresh != null) { if (cardImage != null) cardImage.Dispose(); cardImage = fresh; }
        }
        live = false;
        SetChildren(false);
        if (cardImage == null) { if (Closed != null) Closed(this, EventArgs.Empty); return; }
        float from = t;
        Motion.Run(Tuple.Create(this, "show"), Motion.Fast + 40, e => { t = from * (1 - e); Invalidate(); }, delegate { if (Closed != null) Closed(this, EventArgs.Empty); });
    }

    // The finished card with its children, rounded and ready to blend. Null if the system refuses to print the controls.
    Bitmap PhotographCard() {
        try {
            var card = Card;
            if (card.Width <= 0 || card.Height <= 0) return null;
            capturing = true;
            using (var full = new Bitmap(Width, Height, PixelFormat.Format32bppRgb)) {
                DrawToBitmap(full, new Rectangle(0, 0, Width, Height));
                var image = new Bitmap(card.Width, card.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(image))
                using (var path = Theme.Round(new RectangleF(0, 0, card.Width, card.Height), Theme.S(16)))
                using (var brush = new TextureBrush(full, new Rectangle(card.X, card.Y, card.Width, card.Height))) {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPath(brush, path);
                }
                return image;
            }
        } catch { return null; }
        finally { capturing = false; }
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        if (backdrop != null && Width > 0 && Height > 0) {
            // Re-capture what is behind the card so the dimmed window follows resizing.
            backdrop.Dispose();
            backdrop = TakeBackdrop();
        }
    }

    protected abstract void LayoutCard(Rectangle card);

    protected override void OnLayout(LayoutEventArgs e) {
        base.OnLayout(e);
        if (Controls.Count > 0) LayoutCard(Card);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        if (capturing) { g.Clear(Theme.Surface); return; } // only the card is photographed; children paint over this
        if (backdrop != null) {
            // Copy, not blend: the window behind is opaque.
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImageUnscaled(backdrop, 0, 0);
            g.CompositingMode = CompositingMode.SourceOver;
        } else g.Clear(Theme.Base);
        if (t > 0.002f) using (var veil = new SolidBrush(Color.FromArgb((int)(175 * t), 0, 0, 0))) g.FillRectangle(veil, ClientRectangle);
        Theme.Smooth(g);
        var card = Card;
        if (live || cardImage == null) {
            Theme.Fill(g, card, Theme.S(16), Theme.Surface);
            Theme.Stroke(g, card, Theme.S(16), Theme.Border);
            return;
        }
        // Fade and rise only. Scaling the picture too costs more than the whole rest of the frame, and the
        // fade ends early (alpha reaches 1 at 55%) so the later frames are plain copies.
        float alpha = Math.Min(1, t * 1.8f);
        int rise = (int)Math.Round((1 - Motion.EaseOut(t)) * Theme.S(14));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        if (alpha >= 0.995f) g.DrawImageUnscaled(cardImage, card.X, card.Y + rise);
        else using (var attributes = new ImageAttributes()) {
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
            g.DrawImage(cardImage, new Rectangle(card.X, card.Y + rise, card.Width, card.Height), 0, 0, cardImage.Width, cardImage.Height, GraphicsUnit.Pixel, attributes);
        }
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (rise != 0 || alpha < 0.995f) Theme.Stroke(g, new RectangleF(card.X, card.Y + rise, card.Width, card.Height), Theme.S(16), Color.FromArgb((int)(255 * alpha), Theme.Border));
    }

    // Clicking the dimmed area outside the card closes the modal.
    protected override void OnMouseClick(MouseEventArgs e) {
        if (!Card.Contains(e.Location)) Close();
        base.OnMouseClick(e);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (backdrop != null) backdrop.Dispose();
            if (cardImage != null) cardImage.Dispose();
        }
        base.Dispose(disposing);
    }
}
