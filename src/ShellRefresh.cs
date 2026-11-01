using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public static class ShellRefresh {
    static string lastDeferral="";
    static void Defer(string reason){if(reason!=lastDeferral){lastDeferral=reason;Store.Log("Correção da barra esperando: "+reason);}}
    static string Pending {get{return Path.Combine(Store.Root,"barra-pendente.txt");}}
    static string RecoveryFile {get{return Path.Combine(Store.Root,"pastas-restauracao.xml");}}
    delegate bool Callback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr param);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out DesktopLayout.Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder value,int length);
    [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr window,ref DesktopLayout.Placement placement);
    [DllImport("user32.dll")] static extern bool SetWindowPlacement(IntPtr window,ref DesktopLayout.Placement placement);
    public class Folder {public string Path;public long Handle;public DesktopLayout.Placement Placement;}
    static List<Folder> Folders(){
        var result=new List<Folder>();object com=null,items=null;
        try{com=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));dynamic shell=com;items=shell.Windows();dynamic windows=items;
            for(int i=0;i<(int)windows.Count;i++){object item=null;try{item=windows.Item(i);dynamic w=item;string exe=(string)w.FullName;if(!String.Equals(System.IO.Path.GetFileName(exe),"explorer.exe",StringComparison.OrdinalIgnoreCase))continue;Uri uri;if(!Uri.TryCreate((string)w.LocationURL,UriKind.Absolute,out uri)||!uri.IsFile)throw new Exception("pasta virtual aberta");long handle=(long)w.HWND;var placement=new DesktopLayout.Placement{Length=Marshal.SizeOf(typeof(DesktopLayout.Placement))};GetWindowPlacement(new IntPtr(handle),ref placement);result.Add(new Folder{Path=uri.LocalPath,Handle=handle,Placement=placement});}finally{if(item!=null)Marshal.FinalReleaseComObject(item);}}
        }finally{if(items!=null)Marshal.FinalReleaseComObject(items);if(com!=null)Marshal.FinalReleaseComObject(com);}return result;
    }
    public static bool Ignorable(string cls,int width,int height){return width<=1&&height<=1||cls=="Progman"||cls=="WorkerW"||cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd"||cls=="ThumbnailDeviceHelperWnd"||cls=="DummyDWMListenerWindow"||cls=="EdgeUiInputTopWndClass"&&height<=8;}
    public static bool IsPending {get{return File.Exists(Pending)||File.Exists(RecoveryFile);}}
    public static void Request(){File.WriteAllText(Pending,DateTime.Now.ToString("s"));}
    public static void Cancel(){if(File.Exists(Pending))File.Delete(Pending);}
    static uint ShellPid(){uint id=0;IntPtr shell=GetShellWindow();if(shell!=IntPtr.Zero)GetWindowThreadProcessId(shell,out id);return id;}
    static bool HasExplorerWindows(uint id,List<Folder> folders){
        bool busy=false;
        var blockers=new List<string>();
        var explorerPids=new HashSet<uint>();explorerPids.Add(id);
        foreach(var p in Process.GetProcessesByName("explorer")){using(p){explorerPids.Add((uint)p.Id);}}
        EnumWindows(delegate(IntPtr window,IntPtr data){uint pid;GetWindowThreadProcessId(window,out pid);if(!explorerPids.Contains(pid)||!IsWindowVisible(window))return true;
            var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);string cls=name.ToString();
            DesktopLayout.Rect rect;GetWindowRect(window,out rect);if(Ignorable(cls,rect.Width,rect.Height))return true;
            if(cls=="CabinetWClass"&&folders.Exists(f=>f.Handle==window.ToInt64()))return true;
            {busy=true;blockers.Add(cls+"("+rect.Width+"x"+rect.Height+")");}return true;
        },IntPtr.Zero);if(busy)Defer(blockers.Count==1?"feche a janela do Explorer que está aberta.":"feche as "+blockers.Count+" janelas do Explorer que estão abertas.");return busy;
    }
    public static bool TryRun(string gameDisplay){
        if(!IsPending)return true;
        uint id=ShellPid();if(id==0){Defer("a barra de tarefas ainda não carregou.");return false;}
        try{
            if(File.Exists(RecoveryFile)){if(!RestoreFolders(Store.Read<List<Folder>>(RecoveryFile)))return false;File.Delete(RecoveryFile);Cancel();return true;}
            var folders=Folders();if(HasExplorerWindows(id,folders))return false;
            using(var shell=Process.GetProcessById((int)id)){
                string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
                if(!String.Equals(Native.Exe(shell),expected,StringComparison.OrdinalIgnoreCase)){Defer("a barra de tarefas não é a do Windows.");return false;}
                // Check again immediately before acting, and preserve other monitors across the shell restart.
                var windows=DesktopLayout.Capture(gameDisplay,true,DesktopLayout.GamePids(Store.LoadSettings().Games));folders=Folders();if(HasExplorerWindows(id,folders))return false;
                string recovery=RecoveryFile;Store.Write(recovery,folders);
                shell.Kill();shell.WaitForExit(2000);
                Thread.Sleep(800);
                if(ShellPid()==0)Process.Start(new ProcessStartInfo(expected){UseShellExecute=false});
                for(int i=0;i<50&&ShellPid()==0;i++)Thread.Sleep(100);
                uint next=ShellPid();if(next==0||next==id){Store.Log("A barra de tarefas ainda não voltou depois da correção.", LogKind.Error);return false;}
                bool restored=RestoreFolders(folders);DesktopLayout.Settle(windows);Cancel();lastDeferral="";if(restored&&File.Exists(recovery))File.Delete(recovery);Store.Log("Barra de tarefas recriada"+(folders.Count>0?(restored?"; pastas abertas de novo.":"; algumas pastas não reabriram."):"."));return true;
            }
        }catch(Exception e){Store.Log("Correção da barra adiada: "+e.Message, LogKind.Error);return false;}
    }
    public static bool RestoreFolders(List<Folder> folders){
        bool all=true;var used=new HashSet<long>();
        foreach(var folder in folders){try{Folder found=null;
            foreach(var f in Folders())if(!used.Contains(f.Handle)&&String.Equals(f.Path,folder.Path,StringComparison.OrdinalIgnoreCase)){found=f;break;}
            if(found==null){Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),"/n,\""+folder.Path+"\""){UseShellExecute=true});for(int i=0;i<40&&found==null;i++){Thread.Sleep(100);foreach(var f in Folders())if(!used.Contains(f.Handle)&&String.Equals(f.Path,folder.Path,StringComparison.OrdinalIgnoreCase)){found=f;break;}}}
            if(found==null){all=false;continue;}used.Add(found.Handle);var p=folder.Placement;p.Length=Marshal.SizeOf(typeof(DesktopLayout.Placement));p.Flags|=4;if(p.Show==1)p.Show=4;SetWindowPlacement(new IntPtr(found.Handle),ref p);
        }catch(Exception e){all=false;Store.Log("Não deu para reabrir a pasta "+folder.Path+": "+e.Message, LogKind.Error);}}
        return all;
    }
}
