using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public static class ShellRefresh {
    static Process repairProcess;
    static DateTime repairStarted;
    public static bool IsBusy {get{
        if(repairProcess==null)return false;
        if(!repairProcess.HasExited&&(DateTime.UtcNow-repairStarted).TotalSeconds<60)return true;
        if(!repairProcess.HasExited){repairProcess.Kill();Store.Log("A correção da barra excedeu o tempo permitido; consulte o registro da recuperação independente.",LogKind.Error);}
        repairProcess.Dispose();repairProcess=null;return false;
    }}
    public static void Begin(string display){
        if(IsBusy||!IsPending)return;
        string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(display??""));
        repairProcess=Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,"/shellrefresh "+encoded){UseShellExecute=false,CreateNoWindow=true});
        repairStarted=DateTime.UtcNow;
    }
    static string lastDeferral="";
    static void Defer(string reason){if(reason!=lastDeferral){lastDeferral=reason;Store.Log("Correção da barra esperando: "+reason);}}
    static string Pending {get{return Path.Combine(Store.Root,"barra-pendente.txt");}}
    static string RecoveryFile {get{return Path.Combine(Store.Root,"pastas-restauracao.xml");}}
    delegate bool Callback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr param);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out DesktopLayout.Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder value,int length);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
    // Suspended Store apps keep an invisible frame window; DWM marks it cloaked and the person never sees it.
    static bool Cloaked(IntPtr window){int value;return DwmGetWindowAttribute(window,14,out value,4)==0&&value!=0;}
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
    public struct Status {
        public uint DesktopPid,TrayPid;
        public bool DesktopResponding,TrayResponding;
        public bool Ready {get{return DesktopPid!=0&&DesktopPid==TrayPid&&DesktopResponding&&TrayResponding;}}
    }
    public sealed class ReadinessGate {
        uint pid;long since;
        public bool Observe(Status status,long milliseconds){
            if(!status.Ready){pid=0;return false;}
            if(pid!=status.DesktopPid){pid=status.DesktopPid;since=milliseconds;return false;}
            return milliseconds-since>=750;
        }
    }
    static bool Responds(IntPtr window){IntPtr result;return window!=IntPtr.Zero&&SendMessageTimeout(window,0,IntPtr.Zero,IntPtr.Zero,3,250,out result)!=IntPtr.Zero;}
    static Status CurrentStatus(){
        IntPtr desktop=GetShellWindow(),tray=FindWindow("Shell_TrayWnd",null);uint desktopPid=0,trayPid=0;
        if(desktop!=IntPtr.Zero)GetWindowThreadProcessId(desktop,out desktopPid);
        if(tray!=IntPtr.Zero)GetWindowThreadProcessId(tray,out trayPid);
        return new Status{DesktopPid=desktopPid,TrayPid=trayPid,DesktopResponding=Responds(desktop),TrayResponding=tray!=IntPtr.Zero&&IsWindowVisible(tray)&&Responds(tray)};
    }
    static bool WaitReady(int timeout){
        var clock=Stopwatch.StartNew();var gate=new ReadinessGate();
        while(clock.ElapsedMilliseconds<timeout){if(gate.Observe(CurrentStatus(),clock.ElapsedMilliseconds))return true;Thread.Sleep(100);}return false;
    }
    static string ExplorerPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");}}
    static bool OwnShell(Process process){return process.SessionId==Process.GetCurrentProcess().SessionId&&String.Equals(Native.Exe(process),ExplorerPath,StringComparison.OrdinalIgnoreCase);}
    static void StartExplorer(){Process.Start(new ProcessStartInfo(ExplorerPath){UseShellExecute=true});}

    // Armed before stopping Explorer. Survives a main/worker crash and retries at most once.
    public static void Guard(int oldPid,string eventName){
        if(!eventName.StartsWith("Local\\ResMode.ShellGuard.",StringComparison.Ordinal))return;
        try{
            using(var old=Process.GetProcessById(oldPid)){
                if(!OwnShell(old))return;
                using(var ready=EventWaitHandle.OpenExisting(eventName))ready.Set();
                if(!old.WaitForExit(6000))return; // No restart was performed: leave the working shell alone.
            }
            if(WaitReady(1500))return;
            if(ShellPid()==0)StartExplorer();
            if(WaitReady(8000))return;
            // A PID alone was the old false-success condition. A bounded retry is allowed only
            // for the verified Windows shell with no visible Explorer work/file-copy windows.
            if(File.Exists(Store.State)||DesktopLayout.GamePids(Store.LoadSettings().Games).Count>0){Store.Log("A nova tentativa do Explorer foi adiada porque um jogo abriu.");return;}
            uint current=ShellPid();
            if(current!=0){using(var shell=Process.GetProcessById((int)current)){
                if(!OwnShell(shell)||HasExplorerWindows(current,new List<Folder>()))return;
                shell.Kill();shell.WaitForExit(2000);
            }}
            StartExplorer();
            if(!WaitReady(8000))Store.Log("A área de trabalho não confirmou a recuperação. O reinício não será repetido em loop.",LogKind.Error);
        }catch(Exception e){Store.Log("Falha na recuperação independente do Explorer: "+e.Message,LogKind.Error);}
    }
    static bool HasExplorerWindows(uint id,List<Folder> folders){
        bool busy=false;
        var blockers=new List<string>();
        var explorerPids=new HashSet<uint>();explorerPids.Add(id);
        foreach(var p in Process.GetProcessesByName("explorer")){using(p){explorerPids.Add((uint)p.Id);}}
        EnumWindows(delegate(IntPtr window,IntPtr data){uint pid;GetWindowThreadProcessId(window,out pid);if(!explorerPids.Contains(pid)||!IsWindowVisible(window)||Cloaked(window))return true;
            var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);string cls=name.ToString();
            DesktopLayout.Rect rect;GetWindowRect(window,out rect);if(Ignorable(cls,rect.Width,rect.Height))return true;
            if(cls=="CabinetWClass"&&folders.Exists(f=>f.Handle==window.ToInt64()))return true;
            {busy=true;blockers.Add(cls+"("+rect.Width+"x"+rect.Height+")");}return true;
        },IntPtr.Zero);if(busy)Defer(blockers.Count==1?"feche a janela do Explorer que está aberta.":"feche as "+blockers.Count+" janelas do Explorer que estão abertas.");return busy;
    }
    [DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId,uint flags,IntPtr item1,IntPtr item2);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageTimeoutW")] static extern IntPtr BroadcastSetting(IntPtr window,uint message,IntPtr wParam,string setting,uint flags,uint timeout,out IntPtr result);
    // Icon cache and window metrics notifications: the shell redraws the taskbar without being restarted.
    public static void GentleRefresh(){
        SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero); // SHCNE_ASSOCCHANGED
        foreach(string setting in new[]{"TraySettings","ImmersiveColorSet","WindowMetrics"}){IntPtr result;BroadcastSetting((IntPtr)0xFFFF,0x1A,IntPtr.Zero,setting,2,2000,out result);} // WM_SETTINGCHANGE to everyone
    }
    public static bool TryRun(string gameDisplay){
        if(!IsPending)return true;
        if(File.Exists(Store.State)||DesktopLayout.GamePids(Store.LoadSettings().Games).Count>0){Defer("um jogo está aberto.");return false;}
        uint id=ShellPid();if(!CurrentStatus().Ready){Defer("a área de trabalho e a barra de tarefas ainda não estão prontas.");return false;}
        try{
            if(!WaitReady(2000)){Defer("a área de trabalho ainda está estabilizando.");return false;}
            if(File.Exists(RecoveryFile)){if(!RestoreFolders(Store.Read<List<Folder>>(RecoveryFile)))return false;File.Delete(RecoveryFile);Cancel();return true;}
            // Not a kill and restart of Explorer: Windows logs that as a shell crash, empties the notification area and took
            // Game Bar down with it. Telling the shell that icon and window metrics changed makes it redraw the taskbar and
            // its icons crisp, in place (checked on a real resolution round trip: the icons came back sharp).
            GentleRefresh();
            Cancel();lastDeferral="";
            Store.Log("Ícones da barra de tarefas atualizados.");
            return true;
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

    public static void ReadinessTests(){
        var gate=new ReadinessGate();
        if(gate.Observe(new Status{DesktopPid=7,DesktopResponding=true},5000))throw new Exception("Desktop PID without taskbar was accepted.");
        if(gate.Observe(new Status{DesktopPid=7,TrayPid=8,DesktopResponding=true,TrayResponding=true},5001))throw new Exception("Stale taskbar from a different process was accepted.");
        if(gate.Observe(new Status{DesktopPid=7,TrayPid=7,DesktopResponding=true},5002))throw new Exception("Unresponsive taskbar was accepted.");
        var good=new Status{DesktopPid=7,TrayPid=7,DesktopResponding=true,TrayResponding=true};
        if(gate.Observe(good,6000)||gate.Observe(good,6749)||!gate.Observe(good,6750))throw new Exception("Shell stability interval was not respected.");
        good.DesktopPid=9;good.TrayPid=9;
        if(gate.Observe(good,6800))throw new Exception("Replacement shell inherited the previous shell's stability.");
        gate.Observe(new Status(),7000);
        if(gate.Observe(good,7100)||!gate.Observe(good,7850))throw new Exception("Failed shell readiness did not reset stabilization.");
    }
}
