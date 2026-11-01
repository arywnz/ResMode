using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Read protected history in memory instead of leaving an unencrypted export in Temp.
public static class LogWindow {
    public static void ShowLog(){
        var window=new Form{Text="Histórico — ResMode",StartPosition=FormStartPosition.CenterScreen,Size=new Size(900,600),MinimumSize=new Size(500,350),BackColor=Theme.Base};
        var font=Theme.Mono(10.5f);
        var text=new TextBox{Multiline=true,ReadOnly=true,WordWrap=false,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,BackColor=Theme.Base,ForeColor=Theme.Text,Font=font,BorderStyle=BorderStyle.None};
        try{text.Text=String.Join(Environment.NewLine,Store.ReadLogLines(Store.LogFile).Reverse().Take(2000).Reverse());}
        catch{text.Text="Não foi possível abrir o histórico desta conta do Windows.";}
        window.Controls.Add(text);window.FormClosed+=delegate{font.Dispose();};window.Show();
    }
}
