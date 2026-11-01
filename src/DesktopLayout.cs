using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Linq;
using System.Xml.Serialization;

// Work is limited to the resolution transitions; no continuous window-position polling.
public static class DesktopLayout {
    [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;public int Width{get{return Right-Left;}}public int Height{get{return Bottom-Top;}}}
    [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] public struct Placement {public int Length,Flags,Show;public Point Min,Max;public Rect Normal;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Monitor {
        public int Size;public Rect Bounds,Work;public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;
    }
    public class Saved {
        public long WindowHandle, ProcessStartedTicks;
        [XmlIgnore] public IntPtr Handle { get { return new IntPtr(WindowHandle); } set { WindowHandle=value.ToInt64(); } }
        public uint Pid,Dpi;public string Class,Device;public Rect Bounds,Work,Window;public Placement Placement;
        public bool Minimized,Maximized,UserAdjusted;
    }
    public class DesktopRecovery {public string Device;public int Width,Height;public long SavedUtcTicks=DateTime.UtcNow.Ticks;public List<Saved> Windows=new List<Saved>();}
    static string PendingLayout {get{return Path.Combine(Store.Root,"janelas-restauracao.xml");}}
    delegate void WinEvent(IntPtr hook,uint evt,IntPtr window,int obj,int child,uint thread,uint time);
    static readonly WinEvent moveEvent=Moved;
    static IntPtr moveHook;
    static List<Saved> tracked;
    static int trackedOwner;
    static DesktopRecovery pending;
    static System.Windows.Forms.Timer settling;
    delegate bool WindowCallback(IntPtr window,IntPtr data);
    delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,IntPtr rect,IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr rect,MonitorCallback callback,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref Monitor info);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEvent callback,uint pid,uint thread,uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr window,IntPtr rect,IntPtr region,uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr window,ref Placement placement);
    [DllImport("user32.dll")] static extern bool SetWindowPlacement(IntPtr window,ref Placement placement);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int size);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
    static Monitor Info(IntPtr handle){var m=new Monitor{Size=Marshal.SizeOf(typeof(Monitor))};if(!GetMonitorInfo(handle,ref m))throw new InvalidOperationException("Monitor indisponível.");return m;}
    static string Class(IntPtr window){var b=new StringBuilder(256);GetClassName(window,b,b.Capacity);return b.ToString();}
    static Dictionary<string,Monitor> Monitors(){var result=new Dictionary<string,Monitor>(StringComparer.OrdinalIgnoreCase);EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr h,IntPtr dc,IntPtr r,IntPtr p){var m=Info(h);result[m.Device]=m;return true;},IntPtr.Zero);return result;}
    static uint Dpi(IntPtr window){try{return GetDpiForWindow(window);}catch(EntryPointNotFoundException){return 0;}}
    public static HashSet<uint> GamePids(IEnumerable<GameRule> games){var paths=new HashSet<string>(games.Select(g=>g.Exe),StringComparer.OrdinalIgnoreCase);return new HashSet<uint>(Native.ProcessIds().Where(pid=>paths.Contains(Native.ExePath(pid))).Select(pid=>(uint)pid));}
    public static List<Saved> Capture(string changingDisplay,bool includeChanging=false,ISet<uint> excludedPids=null,bool includeOwn=false){
        var result=new List<Saved>();
        EnumWindows(delegate(IntPtr w,IntPtr unused){try{
            if(!IsWindowVisible(w))return true;
            string cls=Class(w);if(cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd"||cls=="Progman"||cls=="WorkerW")return true;
            long style=GetWindowLongPtr(w,-20).ToInt64();if((style&0x80)!=0||(style&0x08000000)!=0)return true; // tool windows / no-activate overlays
            int cloaked;if(DwmGetWindowAttribute(w,14,out cloaked,4)==0&&cloaked!=0)return true;
            var m=Info(MonitorFromWindow(w,2));if(!includeChanging&&String.Equals(m.Device,changingDisplay,StringComparison.OrdinalIgnoreCase))return true;
            uint pid;GetWindowThreadProcessId(w,out pid);if((!includeOwn&&pid==(uint)Process.GetCurrentProcess().Id)||(excludedPids!=null&&excludedPids.Contains(pid)))return true;
            var placement=new Placement{Length=Marshal.SizeOf(typeof(Placement))};Rect rect;
            if(!GetWindowRect(w,out rect)||!GetWindowPlacement(w,ref placement))return true;
            long started;using(var process=Process.GetProcessById((int)pid))started=process.StartTime.ToUniversalTime().Ticks;
            result.Add(new Saved{Handle=w,Pid=pid,ProcessStartedTicks=started,Class=cls,Device=m.Device,Bounds=m.Bounds,Work=m.Work,Window=rect,Placement=placement,Minimized=IsIconic(w),Maximized=IsZoomed(w),Dpi=Dpi(w)});
        }catch{}return true;},IntPtr.Zero);
        return result;
    }
    public static Rect Translate(Rect rect,int dx,int dy){return new Rect{Left=rect.Left+dx,Top=rect.Top+dy,Right=rect.Right+dx,Bottom=rect.Bottom+dy};}
    public static Rect FitToWork(Rect rect,Rect work){int width=Math.Min(rect.Width,work.Width),height=Math.Min(rect.Height,work.Height);int left=Math.Max(work.Left,Math.Min(rect.Left,work.Right-width)),top=Math.Max(work.Top,Math.Min(rect.Top,work.Bottom-height));return new Rect{Left=left,Top=top,Right=left+width,Bottom=top+height};}
    public static List<Saved> RecoveryWindows(List<Saved> baseline,List<Saved> current){
        var result=new List<Saved>();
        foreach(var old in baseline??new List<Saved>()){
            var now=current.FirstOrDefault(w=>w.WindowHandle==old.WindowHandle&&w.Pid==old.Pid&&w.ProcessStartedTicks==old.ProcessStartedTicks);
            if(now==null)continue;
            result.Add(old.UserAdjusted||old.Minimized!=now.Minimized||old.Maximized!=now.Maximized?now:old);
        }
        foreach(var now in current)if(!result.Any(w=>w.WindowHandle==now.WindowHandle))result.Add(now);
        return result;
    }
    static bool SameWindow(Saved s){
        if(!IsWindow(s.Handle)||!IsWindowVisible(s.Handle)||Class(s.Handle)!=s.Class)return false;
        uint pid;GetWindowThreadProcessId(s.Handle,out pid);if(pid!=s.Pid)return false;
        if(s.ProcessStartedTicks!=0)using(var process=Process.GetProcessById((int)pid))if(process.StartTime.ToUniversalTime().Ticks!=s.ProcessStartedTicks)return false;
        return true;
    }
    public static void Restore(List<Saved> saved,bool onlyWrongMonitor,bool redraw=false){
        var monitors=Monitors();int moved=0,failed=0;
        foreach(var s in saved){try{
            if(s.UserAdjusted||!SameWindow(s))continue;
            Monitor target;if(!monitors.TryGetValue(s.Device,out target))continue; // Never force onto a different monitor if unplugged.
            var current=Info(MonitorFromWindow(s.Handle,2));if(onlyWrongMonitor&&String.Equals(current.Device,s.Device,StringComparison.OrdinalIgnoreCase))continue;
            if(IsIconic(s.Handle)!=s.Minimized)continue; // Respect minimize/restore actions after the snapshot.
            if(IsZoomed(s.Handle)!=s.Maximized)continue; // Do not undo a maximize/restore chosen during play.
            // A DPI change is no reason to leave the window behind: the mode switch itself makes Windows rescale a window
            // (Parsec: 96 -> 144 dpi) while dropping it on the wrong monitor. The saved rectangle belongs to the saved monitor, whose DPI it matches.
            int dx=target.Bounds.Left-s.Bounds.Left,dy=target.Bounds.Top-s.Bounds.Top;
            if(s.Minimized){
                var placement=s.Placement;placement.Length=Marshal.SizeOf(typeof(Placement));placement.Flags|=4;placement.Show=7; // async; minimized without activating
                // WINDOWPLACEMENT normal rect is in workspace coordinates.
                placement.Normal=Translate(placement.Normal,dx-(target.Work.Left-target.Bounds.Left)+(s.Work.Left-s.Bounds.Left),dy-(target.Work.Top-target.Bounds.Top)+(s.Work.Top-s.Bounds.Top));
                if(target.Bounds.Width!=s.Bounds.Width||target.Bounds.Height!=s.Bounds.Height){int wx=target.Work.Left-target.Bounds.Left,wy=target.Work.Top-target.Bounds.Top;placement.Normal=Translate(FitToWork(Translate(placement.Normal,wx,wy),target.Work),-wx,-wy);}
                if(SetWindowPlacement(s.Handle,ref placement))moved++;else failed++;
            }else{
                var rect=Translate(s.Window,dx,dy);
                if(s.Maximized)rect=new Rect{Left=target.Work.Left+s.Window.Left-s.Work.Left,Top=target.Work.Top+s.Window.Top-s.Work.Top,Right=target.Work.Right+s.Window.Right-s.Work.Right,Bottom=target.Work.Bottom+s.Window.Bottom-s.Work.Bottom};
                else if(target.Bounds.Width!=s.Bounds.Width||target.Bounds.Height!=s.Bounds.Height)rect=FitToWork(rect,target.Work);
                Rect now;if(!GetWindowRect(s.Handle,out now))continue;
                if(now.Left==rect.Left&&now.Top==rect.Top&&now.Width==rect.Width&&now.Height==rect.Height){if(redraw)RedrawWindow(s.Handle,IntPtr.Zero,IntPtr.Zero,0x485);continue;}
                // Keep focus, Z order, owner ordering and the window's existing maximize state.
                if(SetWindowPos(s.Handle,IntPtr.Zero,rect.Left,rect.Top,rect.Width,rect.Height,redraw?0x4234u:0x4214u))moved++;else failed++;
                if(redraw)RedrawWindow(s.Handle,IntPtr.Zero,IntPtr.Zero,0x485);
            }
        }catch{failed++;}}
        // Runs up to five times per session; only failures are worth a line in the history.
        if(failed>0)Store.Log(failed==1?"Uma janela não voltou para o lugar.":failed+" janelas não voltaram para o lugar.",LogKind.Error);
    }
    public static void Settle(List<Saved> saved){
        Thread.Sleep(200);Restore(saved,false);
        Thread.Sleep(450);Restore(saved,false);
        Thread.Sleep(650);Restore(saved,false,true);
    }

    // Event-driven: no background polling of application window positions.
    public static void Track(List<Saved> windows,int ownerPid){
        StopTracking();tracked=windows;trackedOwner=ownerPid;
        moveHook=SetWinEventHook(0xA,0xB,IntPtr.Zero,moveEvent,0,0,0);
        if(moveHook==IntPtr.Zero){foreach(var w in windows)w.UserAdjusted=true;Store.Log("Não deu para acompanhar as janelas durante o jogo; elas vão ficar onde estiverem.",LogKind.Error);PersistAdjustments();}
    }
    static void Moved(IntPtr hook,uint evt,IntPtr window,int obj,int child,uint thread,uint time){
        if(tracked==null||window==IntPtr.Zero||obj!=0||child!=0)return;
        try{var s=tracked.FirstOrDefault(w=>w.Handle==window);if(s==null||s.UserAdjusted||!SameWindow(s))return;s.UserAdjusted=true;PersistAdjustments();}catch(Exception e){Store.Log("Erro ao acompanhar uma janela: "+e.Message,LogKind.Error);}
    }
    static void PersistAdjustments(){
        if(trackedOwner!=0&&File.Exists(Store.State)){var r=Store.Read<Recovery>(Store.State);if(r.OwnerPid!=trackedOwner)return;foreach(var old in r.Windows)if(tracked.Any(w=>w.WindowHandle==old.WindowHandle&&w.UserAdjusted))old.UserAdjusted=true;Store.Write(Store.State,r);}
        else if(pending!=null&&File.Exists(PendingLayout))Store.Write(PendingLayout,pending);
    }
    public static void StopTracking(){if(moveHook!=IntPtr.Zero){UnhookWinEvent(moveHook);moveHook=IntPtr.Zero;}tracked=null;trackedOwner=0;}
    public static void CancelDesktopRestore(){
        if(settling!=null){settling.Stop();settling.Dispose();settling=null;}pending=null;StopTracking();if(File.Exists(PendingLayout))File.Delete(PendingLayout);
    }
    public static void SaveDesktopRecovery(string device,int width,int height,List<Saved> windows){Store.Write(PendingLayout,new DesktopRecovery{Device=device,Width=width,Height=height,Windows=windows});}
    public static void ResumeDesktopRestore(){
        if(settling!=null||!File.Exists(PendingLayout)||File.Exists(Store.State))return;
        pending=Store.Read<DesktopRecovery>(PendingLayout);
        if(DateTime.UtcNow.Ticks-pending.SavedUtcTicks>TimeSpan.FromMinutes(1).Ticks||!DesktopModeMatches()){CancelDesktopRestore();return;}
        Restore(pending.Windows,false,true);
        if(!System.Windows.Forms.Application.MessageLoop){Settle(pending.Windows);CancelDesktopRestore();return;}
        Track(pending.Windows,0);
        int pass=0;var delays=new[]{250,500,1000,1500};
        settling=new System.Windows.Forms.Timer{Interval=delays[0]};
        settling.Tick+=delegate{
            try{if(File.Exists(Store.State)||!DesktopModeMatches()){CancelDesktopRestore();return;}Restore(pending.Windows,false,true);pass++;if(pass>=delays.Length)CancelDesktopRestore();else settling.Interval=delays[pass];}
            catch(Exception e){Store.Log("Erro ao reposicionar as janelas: "+e.Message,LogKind.Error);CancelDesktopRestore();}
        };settling.Start();
    }
    static bool DesktopModeMatches(){var m=Native.Current(pending.Device);return m.width==pending.Width&&m.height==pending.Height;}
    public static void GeometryTests(){
        var r=new Rect{Left=2000,Top=80,Right=2800,Bottom=680};var shifted=Translate(r,960,0);
        if(shifted.Left!=2960||shifted.Right!=3760||shifted.Width!=800||shifted.Height!=600)throw new Exception("Translação para monitor à direita incorreta.");
        var restored=Translate(shifted,-960,0);if(restored.Left!=r.Left||restored.Top!=r.Top||restored.Right!=r.Right)throw new Exception("Restauração de coordenadas incorreta.");
        var left=Translate(new Rect{Left=-1800,Top=-50,Right=-1000,Bottom=550},0,0);if(left.Left!=-1800||left.Top!=-50)throw new Exception("Coordenadas negativas alteradas.");
        if(Marshal.SizeOf(typeof(Placement))!=44)throw new Exception("WINDOWPLACEMENT inválido.");
    }
}
