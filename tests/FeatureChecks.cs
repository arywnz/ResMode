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
        var tintMethod=typeof(Artwork).GetMethod("Tint",BindingFlags.NonPublic|BindingFlags.Static);
        using(var green=new Bitmap(148,222)){
            using(var g=Graphics.FromImage(green))g.Clear(Color.Lime);
            var tint=(Color)tintMethod.Invoke(null,new object[]{green});Check(tint.R==0&&tint.G==255&&tint.B==0,"Cor dominante foi artificialmente alterada.");
        }
        using(var grid=new Grid{Size=new Size(1120,650)}){
            var size=GameGrid.Cover;
            using(var cover=new Bitmap(size.Width,size.Height)){
                for(int y=0;y<cover.Height;y++)for(int x=0;x<cover.Width;x++)cover.SetPixel(x,y,(x+y)%2==0?Color.White:Color.Black);
                var art=new Artwork.Art{Cover=cover,Tint=Color.Red};
                var game=new LibraryGame{Key="fixture:sharp",Name="Sharp cover",Launcher=Launchers.Manual,Installed=true,Rules=new List<GameRule>()};
                var tile=(Bitmap)typeof(GameGrid).GetMethod("HoverTile",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(grid,new object[]{game,art,1f,false});
                int m=Artwork.CardMargin;
                foreach(var at in new[]{new Point(12,12),new Point(13,12),new Point(size.Width-14,size.Height-14)})Check(tile.GetPixel(m+at.X,m+at.Y).ToArgb()==cover.GetPixel(at.X,at.Y).ToArgb(),"Hover changed sharp cover pixels.");
                var backdrop=tile.GetPixel(m-4,m+size.Height+25);Check(backdrop.A>=90&&backdrop.R>220&&backdrop.G==0&&backdrop.B==0,"Card background is missing, weak or unrelated to cover colour.");
                Check(tile.GetPixel(0,0).A==0,"Blur spilled outside the bounded card background.");
            }
        }
        results.Add("PASS: visible cover-colour background behind the card, unchanged sharp cover pixels and no blurred halo outside its bounds.");
        using(var grid=new Grid{Size=new Size(1120,650)}){
            var games=Enumerable.Range(0,5).Select(i=>new LibraryGame{Key="fixture:"+i,Name="Fixture "+i,Launcher=Launchers.Manual,Installed=true,Rules=new List<GameRule>()}).ToList();grid.SetGames(games,false);
            var first=Card(grid,0);var last=Card(grid,4);grid.Pointer(new Point(first.X+30,first.Y+30));grid.Pointer(new Point(last.X+30,last.Y+30));
            Check(Hover(grid).Count(pair=>pair.Value>=.3f)>=5,"Movimento rápido não iluminou os cards atravessados.");
            grid.LeaveGrid();Pump(220);Check(Hover(grid).Count==0,"Brilho ressurgiu depois de sair da biblioteca.");
            float value=0;Motion.To("zero-distance",0,1,100,v=>value=v);Motion.To("zero-distance",0,0,100,v=>value=v);Pump(180);Check(value==0,"Tween antigo não cancelado na troca instantânea.");
            grid.Pointer(new Point(first.X+30,first.Y+30));grid.SetGames(games,false);Pump(180);Check(Hover(grid).Count==0,"Troca de aba preservou um hover antigo.");
        }
        results.Add("PASS: iluminação imediata em cinco cards atravessados, limpeza ao sair/trocar aba e cancelamento de tween antigo sem distância.");
        File.WriteAllLines(args[0],results);
    }
}
