using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public static class FeatureChecks {
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr item);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern int GetTextFace(IntPtr dc,int count,StringBuilder value);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr item);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    class Grid:GameGrid {public void Pointer(Point at){OnMouseMove(new MouseEventArgs(MouseButtons.None,0,at.X,at.Y,0));}public void LeaveGrid(){OnMouseLeave(EventArgs.Empty);}}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Pump(int ms){var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
    static Dictionary<string,float> Hover(Grid grid){return (Dictionary<string,float>)typeof(GameGrid).GetField("cardHover",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(grid);}
    static Rectangle Card(Grid grid,int i){return (Rectangle)typeof(GameGrid).GetMethod("CardRect",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(grid,new object[]{i});}
    [STAThread] public static void Main(string[] args){try{Run(args);}catch(Exception e){File.WriteAllText(args[0]+".error",e.ToString());Environment.ExitCode=1;}}
    static void Run(string[] args){
        Native.SetProcessDPIAware();Application.EnableVisualStyles();var results=new List<string>();
        using(var body=Theme.Body(10))using(var title=Theme.Title(18))using(var mono=Theme.Mono(11,true)){
            Check(body.Name=="Geist"&&title.Name=="Geist SemiBold"&&mono.Name=="Geist Mono Medium","Famílias Geist locais não foram carregadas.");
            IntPtr dc=CreateCompatibleDC(IntPtr.Zero),font=body.ToHfont(),old=SelectObject(dc,font);var actual=new StringBuilder(100);GetTextFace(dc,100,actual);SelectObject(dc,old);DeleteObject(font);DeleteDC(dc);
            Check(actual.ToString().StartsWith("Geist",StringComparison.Ordinal),"TextRenderer/GDI está usando uma fonte substituta.");
            int a=TextRenderer.MeasureText("111111",mono,Size.Empty,TextFormatFlags.NoPadding).Width,b=TextRenderer.MeasureText("888888",mono,Size.Empty,TextFormatFlags.NoPadding).Width;Check(a==b,"Fonte técnica não é monoespaçada.");
            results.Add("PASS: Geist Sans/Medium/SemiBold e Geist Mono incorporadas; GDI confirmou Geist e números monoespaçados.");
        }
        Check(Updater.Compare("0.2.2-alpha.1","0.2.2-alpha.2")<0&&Updater.Compare("0.2.2-alpha.2","0.2.2")<0&&Updater.Compare("0.2.2","0.2.10")<0&&Updater.Compare("0.2.2-alpha.10","0.2.2-alpha.9")>0&&Updater.Compare("1.0.0","1.0.0")==0&&Updater.Compare("0.2.2-beta.1","0.2.2-alpha.9")>0,"Ordem de versões incorreta.");
        {
            string good="https://github.com/arywnz/ResMode/releases/download/v9.0.0/ResMode-9.0.0-win-x64.msi",sum=new string('a',64);
            Func<string,string,string,string> release=(tag,url,digest)=>"{\"tag_name\":\""+tag+"\",\"draft\":false,\"html_url\":\"https://github.com/arywnz/ResMode/releases/tag/"+tag+"\",\"assets\":[{\"name\":\"ResMode-"+tag.Substring(1)+"-win-x64.msi\",\"browser_download_url\":\""+url+"\",\"size\":1000,\"digest\":\""+digest+"\"}]}";
            var offer=Updater.Pick("["+release("v9.0.0",good,"sha256:"+sum)+","+release("v8.0.0",good.Replace("9.0.0","8.0.0"),"sha256:"+sum)+",{\"tag_name\":\"v10.0.0\",\"draft\":true},{\"tag_name\":\"nightly\"},"+release("v0.1.0",good,"sha256:"+sum)+"]");
            Check(offer!=null&&offer.Version=="9.0.0"&&offer.CanDownload&&offer.Sha256==sum,"Não escolheu a versão mais nova publicada.");
            Check(Updater.Pick("["+release("v9.0.0","https://example.com/ResMode-9.0.0-win-x64.msi","sha256:"+sum)+"]").CanDownload==false,"Aceitou instalador fora do GitHub do projeto.");
            Check(Updater.Pick("["+release("v9.0.0",good,"md5:abc")+"]").CanDownload==false,"Aceitou instalador sem SHA-256.");
            Check(Updater.Pick("["+release("v"+AppInfo.Release,good,"sha256:"+sum)+"]")==null,"Ofereceu a própria versão instalada.");
            results.Add("PASS: Atualizador compara versões, ignora rascunhos e só aceita MSI do repositório com SHA-256.");
        }
        {
            string root=Path.Combine(Path.GetTempPath(),"resmode-finder-"+Guid.NewGuid().ToString("N"));
            Action<string,long> file=(p,size)=>{string full=Path.Combine(root,p);Directory.CreateDirectory(Path.GetDirectoryName(full));using(var f=File.Create(full))f.SetLength(size);};
            try{
                file(@"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",5000000);file(@"Epic Games\Launcher\Portal\Binaries\Win64\EOSSDK-Win64-Shipping.dll",300000);
                file(@"Epic Games\Epic Online Services\EOSOverlay\EOSOverlayRenderer-Win64-Shipping.exe",5000000);file(@"Epic Games\Epic Online Services\EOSOverlay\EOSSDK-Win64-Shipping.dll",300000);
                file(@"Jogos\Meu Jogo\MeuJogo.exe",4000000);file(@"Jogos\Ferramenta\ferramenta.exe",500000);
                file(@"Outros\Cool Game\CoolGame.exe",300000);file(@"Outros\Cool Game\UnityPlayer.dll",300000);
                file(@"Outros\Steam Dir\Game.exe",300000);file(@"Outros\Steam Dir\steam_appid.txt",10);
                var found=GameFinder.FromPath(root,x=>false);
                var names=found.Select(g=>Path.GetFileName(g.Folder)).ToList();
                Check(!found.Any(g=>g.Folder.IndexOf("Epic",StringComparison.OrdinalIgnoreCase)>=0),"Achou o launcher ou os serviços da Epic como jogo: "+String.Join(", ",names));
                Check(names.Contains("Meu Jogo")&&names.Contains("Cool Game")&&names.Contains("Steam Dir"),"Não achou os jogos esperados: "+String.Join(", ",names));
                Check(!names.Contains("Ferramenta"),"Tomou um programa pequeno por jogo.");
                results.Add("PASS: Busca ignora o launcher da Epic e acha jogos em pasta de jogos, Unity e steam_appid.txt.");
            }finally{try{Directory.Delete(root,true);}catch{}}
        }
        var tintMethod=typeof(Artwork).GetMethod("Tint",BindingFlags.NonPublic|BindingFlags.Static);
        using(var green=new Bitmap(148,222)){
            using(var g=Graphics.FromImage(green))g.Clear(Color.Lime);
            var tint=(Color)tintMethod.Invoke(null,new object[]{green});Check(tint.R==0&&tint.G==255&&tint.B==0,"Cor dominante foi artificialmente alterada.");
        }
        using(var grid=new Grid{Size=new Size(Theme.S(1120),Theme.S(650))}){
            var size=GameGrid.Cover;
            using(var cover=new Bitmap(size.Width,size.Height)){
                for(int y=0;y<cover.Height;y++)for(int x=0;x<cover.Width;x++)cover.SetPixel(x,y,(x+y)%2==0?Color.White:Color.Black);
                var art=new Artwork.Art{Cover=cover,Tint=Color.Red};
                var game=new LibraryGame{Key="fixture:sharp",Name="Sharp cover",Launcher=Launchers.Manual,Installed=true,Rules=new List<GameRule>()};
                var tile=(Bitmap)typeof(GameGrid).GetMethod("HoverTile",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(grid,new object[]{game,art,1f,false});
                int m=Artwork.CardMargin;
                foreach(var at in new[]{new Point(12,12),new Point(13,12),new Point(size.Width-14,size.Height-14)})Check(tile.GetPixel(m+at.X,m+at.Y).ToArgb()==cover.GetPixel(at.X,at.Y).ToArgb(),"Hover changed sharp cover pixels.");
                var near=tile.GetPixel(m-2,m+size.Height/2);var far=tile.GetPixel(m-6,m+size.Height/2);
                Check(near.A>=160&&near.A<245&&near.R>220&&near.G==0&&near.B==0,"Cover glow must be clearly visible and retain the cover colour.");
                Check(far.A>=40&&far.A<near.A,"Cover glow must visibly spread and fade outward instead of forming a solid outline.");
                Check(tile.GetPixel(m-2,m+size.Height+12).A==0,"Cover colour leaked into the name/platform area.");
                Check(tile.GetPixel(0,0).A==0,"Blur spilled outside the bounded card background.");
            }
        }
        using(var mostlyBlue=new Bitmap(160,240)){
            using(var g=Graphics.FromImage(mostlyBlue)){
                for(int y=0;y<24;y++)using(var brush=new SolidBrush(Color.FromArgb(24+y*2,30+y*2,45+y*2)))g.FillRectangle(brush,0,y*10,160,10);
                g.FillRectangle(Brushes.Gold,0,0,160,40);
            }
            var tint=(Color)tintMethod.Invoke(null,new object[]{mostlyBlue});Check(tint.B>tint.R&&tint.B>tint.G,"A small gold title displaced the dominant blue cover tones.");
        }
        results.Add("PASS: soft cover-only glow with outward falloff, unchanged cover pixels, no colour behind labels and dominant image tones preferred over a small bright title.");
        using(var grid=new Grid{Size=new Size(Theme.S(1120),Theme.S(650))}){
            var games=Enumerable.Range(0,5).Select(i=>new LibraryGame{Key="fixture:"+i,Name="Fixture "+i,Launcher=Launchers.Manual,Installed=true,Rules=new List<GameRule>()}).ToList();grid.SetGames(games,false);
            var first=Card(grid,0);var last=Card(grid,4);grid.Pointer(new Point(first.X+30,first.Y+30));grid.Pointer(new Point(last.X+30,last.Y+30));
            Check(Hover(grid).Count(pair=>pair.Value>=.3f)>=5,"Movimento rápido não iluminou os cards atravessados: "+String.Join(", ",Hover(grid).Select(pair=>pair.Key+"="+pair.Value.ToString("0.00"))));
            grid.LeaveGrid();Pump(220);Check(Hover(grid).Count==0,"Brilho ressurgiu depois de sair da biblioteca.");
            float value=0;Motion.To("zero-distance",0,1,100,v=>value=v);Motion.To("zero-distance",0,0,100,v=>value=v);Pump(180);Check(value==0,"Tween antigo não cancelado na troca instantânea.");
            grid.Pointer(new Point(first.X+30,first.Y+30));grid.SetGames(games,false);Pump(180);Check(Hover(grid).Count==0,"Troca de aba preservou um hover antigo.");
        }
        results.Add("PASS: iluminação imediata em cinco cards atravessados, limpeza ao sair/trocar aba e cancelamento de tween antigo sem distância.");
        File.WriteAllLines(args[0],results);
    }
}
