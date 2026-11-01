using System;
using System.Drawing;
using System.Windows.Forms;

// Card floating over the dimmed window. Subclasses add controls and place them in LayoutCard.
public abstract class Modal : Painted {
    readonly Func<Bitmap> snapshot;
    Bitmap backdrop;
    protected float t;
    bool closing;
    protected Size MaxCard = new Size(Theme.S(780), Theme.S(660));

    public event EventHandler Closed;

    protected Modal(Func<Bitmap> windowSnapshot) {
        snapshot = windowSnapshot;
        BackColor = Theme.Surface; // children paint on the card color
    }

    protected Label CardLabel(string text, Font font, Color color) {
        return new Label { Text = text, Font = font, ForeColor = color, BackColor = Theme.Surface, AutoSize = false };
    }

    protected Rectangle Card {
        get {
            int w = Math.Min(MaxCard.Width, Width - Theme.S(64)), h = Math.Min(MaxCard.Height, Height - Theme.S(56));
            int rise = (int)((1 - t) * Theme.S(18));
            return new Rectangle((Width - w) / 2, (Height - h) / 2 + rise, w, h);
        }
    }

    public void Open() {
        backdrop = snapshot();
        foreach (Control c in Controls) c.Visible = false;
        bool shown = false;
        Motion.Run(Tuple.Create(this, "show"), Motion.Normal + 40, e => {
            t = e;
            if (e > 0.55f && !shown) {
                shown = true;
                foreach (Control c in Controls) c.Visible = true;
                PerformLayout();
                OnShown();
            }
            PerformLayout();
            Invalidate();
        });
    }

    protected virtual void OnShown() { }
    protected virtual void OnClosing() { }

    public void Close() {
        if (closing) return;
        closing = true;
        OnClosing();
        foreach (Control c in Controls) c.Visible = false;
        float from = t;
        Motion.Run(Tuple.Create(this, "show"), Motion.Fast, e => { t = from * (1 - e); Invalidate(); }, () => { if (Closed != null) Closed(this, EventArgs.Empty); });
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        if (backdrop != null && Width > 0 && Height > 0) {
            // Re-capture what is behind the card so the dimmed window follows resizing.
            backdrop.Dispose();
            backdrop = snapshot();
        }
    }

    protected abstract void LayoutCard(Rectangle card);

    protected override void OnLayout(LayoutEventArgs e) {
        base.OnLayout(e);
        if (Controls.Count > 0) LayoutCard(Card);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        if (backdrop != null) g.DrawImageUnscaled(backdrop, 0, 0); else g.Clear(Theme.Base);
        using (var veil = new SolidBrush(Color.FromArgb((int)(175 * t), 0, 0, 0))) g.FillRectangle(veil, ClientRectangle);
        Theme.Smooth(g);
        var card = Card;
        int alpha = (int)(255 * Math.Min(1, t * 1.6f));
        Theme.Fill(g, card, Theme.S(16), Color.FromArgb(alpha, Theme.Surface));
        Theme.Stroke(g, card, Theme.S(16), Color.FromArgb(alpha, Theme.Border));
    }

    // Clicking the dimmed area outside the card closes the modal.
    protected override void OnMouseClick(MouseEventArgs e) {
        if (!Card.Contains(e.Location)) Close();
        base.OnMouseClick(e);
    }

    protected override void Dispose(bool disposing) {
        if (disposing && backdrop != null) backdrop.Dispose();
        base.Dispose(disposing);
    }
}
