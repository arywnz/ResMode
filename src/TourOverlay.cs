using System;
using System.Drawing;
using System.Windows.Forms;

// First-run walkthrough: four short steps on what the app does, how to get a larger resolution, the library and the tray.
// Opens by itself the first time the window shows; "Rever guia" in Settings brings it back.
public class TourOverlay : Modal {
    class Step { public string Title; public Func<string> Body; }

    class StepDots : Painted {
        public int Count, Index;
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics;
            g.Clear(ParentColor);
            Theme.Smooth(g);
            int d = Theme.S(8), gap = Theme.S(8);
            for (int i = 0; i < Count; i++) {
                bool on = i == Index;
                var r = new Rectangle(i * (d + gap) + (on ? 0 : 0), (Height - d) / 2, on ? d * 3 : d, d);
                // The current step is a longer pill.
                int x = 0;
                for (int k = 0; k < i; k++) x += (k == Index ? d * 3 : d) + gap;
                r.X = x;
                Theme.Fill(g, r, d / 2f, on ? Theme.Accent : Theme.Border);
            }
        }
    }

    readonly AppContext owner;
    readonly Label title, body, counter;
    readonly FlatButton next, back, skip, guide;
    readonly StepDots dots;
    readonly Step[] steps;
    int step;

    public TourOverlay(AppContext context, Func<Bitmap> windowSnapshot) : base(windowSnapshot) {
        owner = context;
        MaxCard = new Size(Theme.S(540), Theme.S(370));
        steps = new[] {
            new Step { Title = "Bem-vindo ao ResMode", Body = () =>
                "O ResMode liga uma resolução maior quando você abre um jogo e devolve a resolução normal do Windows quando você fecha. Você joga mais nítido e o desktop não fica estranho o dia inteiro." },
            new Step { Title = "Uma resolução maior", Body = () => {
                var cfg = owner.Configuration;
                return cfg.Width > 0
                    ? "Seu monitor já oferece resoluções maiores que a dele, e o ResMode escolheu " + cfg.Width + " × " + cfg.Height + ". Dá para trocar nas Configurações, e o botão Testar mostra por alguns segundos como fica."
                    : "Seu monitor ainda não oferece uma resolução maior. Ative o DSR (NVIDIA) ou o VSR (AMD) no painel da placa de vídeo. O passo a passo abaixo abre o painel certo para a sua placa e confere quando você terminar.";
            } },
            new Step { Title = "Sua biblioteca", Body = () =>
                "Os jogos da Steam, Epic, GOG e outros aparecem sozinhos. Clique em um jogo para jogar, ignorá-lo ou escolher uma resolução só para ele. Jogos que ficam fora de launchers são procurados no PC e dá para adicionar à mão." },
            new Step { Title = "Pode fechar a janela", Body = () =>
                "O ResMode continua na bandeja, perto do relógio, e cuida do resto sozinho. O ícone mostra o estado, e um aviso aparece aqui quando sai uma versão nova. Você pode rever este guia nas Configurações." }
        };

        counter = CardLabel("", Theme.Body(10), Theme.Faint);
        title = CardLabel("", Theme.Title(18), Theme.Text);
        body = CardLabel("", Theme.Body(11), Theme.Muted);
        dots = new StepDots { Count = steps.Length, BackColor = Theme.Surface };
        next = new FlatButton { Kind = ButtonKind.Primary };
        back = new FlatButton { Text = "Voltar", Kind = ButtonKind.Quiet };
        back.Width = back.PreferredWidth();
        skip = new FlatButton { Text = "Pular", Kind = ButtonKind.Quiet };
        skip.Width = skip.PreferredWidth();
        next.Click += delegate { if (step >= steps.Length - 1) Close(); else ShowStep(step + 1); };
        back.Click += delegate { if (step > 0) ShowStep(step - 1); };
        skip.Click += delegate { Close(); };
        // Only on the resolution step, and only when the driver offers nothing larger yet.
        guide = new FlatButton { Text = "Ativar passo a passo", Kind = ButtonKind.Secondary };
        guide.Click += delegate { owner.RequestGuide(); Close(); };
        Controls.AddRange(new Control[] { counter, title, body, dots, skip, back, next, guide });
        ShowStep(0);
    }

    void ShowStep(int index) {
        step = index;
        counter.Text = "Passo " + (index + 1) + " de " + steps.Length;
        title.Text = steps[index].Title;
        body.Text = steps[index].Body();
        dots.Index = index;
        dots.Invalidate();
        next.Text = index == steps.Length - 1 ? "Começar" : "Próximo";
        next.Width = Math.Max(next.PreferredWidth(), Theme.S(110));
        // Zero width rather than Visible: the modal turns every child visible again when it opens.
        back.Width = index > 0 ? back.PreferredWidth() : 0;
        skip.Width = index < steps.Length - 1 ? skip.PreferredWidth() : 0;
        guide.Width = index == 1 && owner.Configuration.Width == 0 ? Math.Max(guide.PreferredWidth(), Theme.S(180)) : 0;
        PerformLayout();
        Invalidate(true);
    }

    protected override void OnClosing() { owner.MarkTourDone(); }

    protected override void LayoutCard(Rectangle card) {
        int pad = Theme.S(36), x = card.X + pad, w = card.Width - pad * 2;
        counter.SetBounds(x, card.Y + Theme.S(34), w, Theme.S(20));
        title.SetBounds(x, card.Y + Theme.S(58), w, Theme.S(40));
        body.SetBounds(x, card.Y + Theme.S(116), w, Theme.S(130));
        dots.SetBounds(x, card.Y + Theme.S(240), Theme.S(120), Theme.S(20));
        guide.Location = new Point(x, card.Y + Theme.S(186));
        int by = card.Bottom - Theme.S(28) - next.Height;
        next.Location = new Point(card.Right - pad - next.Width, by);
        back.Location = new Point(next.Left - Theme.S(8) - back.Width, by);
        skip.Location = new Point(x - Theme.S(10), by);
    }
}
