using System;
using System.Drawing;
using System.Windows.Forms;

// "Ativar resolução maior": the steps for this PC's graphics card, a button that opens its control panel and one that
// checks again once the person has switched DSR/VSR on.
public class DriverGuide : Modal {
    readonly AppContext owner;
    readonly GpuHelper.Maker maker;
    readonly Label title, intro, steps, status;
    readonly FlatButton openPanel, verify, closeButton;

    public DriverGuide(AppContext context, Func<Bitmap> windowSnapshot, GpuHelper.Maker? force = null) : base(windowSnapshot) {
        owner = context;
        maker = force ?? GpuHelper.Detect();
        MaxCard = new Size(Theme.S(600), Theme.S(470));
        title = CardLabel("Ativar uma resolução maior", Theme.Title(18), Theme.Text);
        title.AutoSize = true;
        closeButton = new FlatButton { Glyph = Theme.IconClose, Kind = ButtonKind.Quiet, Width = Theme.S(36) };
        closeButton.Click += delegate { Close(); };
        intro = CardLabel(Intro(), Theme.Body(10.5f), Theme.Muted);
        steps = CardLabel(Steps(), Theme.Body(11), Theme.Text);
        openPanel = new FlatButton { Text = GpuHelper.ButtonText(maker), Kind = ButtonKind.Primary };
        openPanel.Width = openPanel.PreferredWidth();
        openPanel.Enabled = maker != GpuHelper.Maker.Intel;
        openPanel.Click += delegate {
            string message;
            GpuHelper.OpenPanel(maker, out message);
            Say(message, false);
        };
        verify = new FlatButton { Text = "Já ativei, verificar" };
        verify.Width = verify.PreferredWidth();
        verify.Click += delegate {
            try {
                string result = owner.RecheckModes();
                Say(result, result.IndexOf("encontrada", StringComparison.Ordinal) >= 0 || result.StartsWith("Já está", StringComparison.Ordinal));
            } catch (Exception e) { Say(e.Message, false); }
        };
        status = CardLabel("", Theme.Body(10.5f), Theme.Muted);
        Controls.AddRange(new Control[] { title, closeButton, intro, steps, openPanel, verify, status });
    }

    string Intro() {
        return maker == GpuHelper.Maker.Unknown
            ? "O ResMode usa as resoluções que o driver da placa de vídeo oferece. Procure no painel dela a opção de resolução virtual."
            : "Placa de vídeo: " + GpuHelper.Name(maker) + ". O ResMode só usa as resoluções que o driver oferece, e a maior delas fica ligada nos jogos.";
    }

    string Steps() {
        switch (maker) {
            case GpuHelper.Maker.Nvidia:
                return "1   Clique em \"Abrir o painel da NVIDIA\" (ou clique com o botão direito na área de trabalho › Painel de Controle NVIDIA).\n\n" +
                       "2   Em \"Gerenciar configurações 3D\", abra \"Configurações globais\".\n\n" +
                       "3   Em \"DSR - Fatores\", marque 2,25× (DL) e 1,78× (DL). Para a mais alta, marque também 4,00×.\n\n" +
                       "4   Clique em \"Aplicar\", volte aqui e clique em \"Já ativei, verificar\".\n\n" +
                       "No NVIDIA app: Gráficos › Configurações globais › Fatores DSR.";
            case GpuHelper.Maker.Amd:
                return "1   Clique em \"Abrir o AMD Software\" (Adrenalin).\n\n" +
                       "2   Abra Configurações (a engrenagem) › Tela.\n\n" +
                       "3   Ligue \"Super Resolução Virtual\".\n\n" +
                       "4   Volte aqui e clique em \"Já ativei, verificar\".";
            case GpuHelper.Maker.Intel:
                return "As placas Intel não têm resolução virtual no Windows. Dá para criar uma resolução personalizada no Intel Graphics Command Center, em Tela › Resolução personalizada, e depois clicar em \"Já ativei, verificar\".";
            default:
                return "1   Abra o painel da sua placa de vídeo (NVIDIA ou AMD).\n\n" +
                       "2   Procure DSR (NVIDIA) ou Super Resolução Virtual (AMD) e ative.\n\n" +
                       "3   Volte aqui e clique em \"Já ativei, verificar\".";
        }
    }

    void Say(string text, bool good) {
        status.ForeColor = good ? Theme.Live : Theme.Muted;
        status.Text = text;
    }

    protected override void LayoutCard(Rectangle card) {
        int pad = Theme.S(34), x = card.X + pad, w = card.Width - pad * 2;
        title.Location = new Point(x - Theme.S(2), card.Y + Theme.S(28));
        closeButton.Location = new Point(card.Right - Theme.S(18) - closeButton.Width, card.Y + Theme.S(22));
        intro.SetBounds(x, card.Y + Theme.S(76), w, Theme.S(44));
        steps.SetBounds(x, card.Y + Theme.S(132), w, Theme.S(236));
        int by = card.Bottom - Theme.S(30) - openPanel.Height;
        status.SetBounds(x, by - Theme.S(34), w, Theme.S(28));
        openPanel.Location = new Point(x, by);
        verify.Location = new Point(openPanel.Right + Theme.S(10), by);
    }
}
