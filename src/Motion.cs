using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Frame waits happen off the UI thread. Only one frame can be queued at a time.
public static class Motion {
    class Tween { public object Key; public long Start; public double Ms; public Action<float> Step; public Action Done; }

    static readonly List<Tween> tweens = new List<Tween>();
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static readonly HashSet<Control> repaints = new HashSet<Control>();
    static readonly AutoResetEvent wake = new AutoResetEvent(false);
    static SynchronizationContext context;
    static Thread frameThread;
    static volatile bool running, shuttingDown;
    static int framePosted;
    static double frameMilliseconds = 1000.0 / 120;
    static long nextDisplayCheck;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateWaitableTimerEx(IntPtr attributes,string name,uint flags,uint access);
    [DllImport("kernel32.dll")] static extern bool SetWaitableTimer(IntPtr timer,ref long due,int period,IntPtr callback,IntPtr argument,bool resume);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

    static void Start() {
        if (shuttingDown || running) return;
        if (frameThread == null) {
            context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            frameThread = new Thread(FrameLoop, 256 * 1024) { IsBackground = true, Name = "ResMode animation clock" };
            Application.ApplicationExit += delegate { shuttingDown = true; running = false; wake.Set(); };
            frameThread.Start();
        }
        if (clock.ElapsedMilliseconds >= nextDisplayCheck) {
            try { frameMilliseconds = 1000.0 / Math.Min(180, Math.Max(60, Native.Current(Screen.PrimaryScreen.DeviceName).hz)); } catch { }
            nextDisplayCheck = clock.ElapsedMilliseconds + 1000;
        }
        running = true;
        wake.Set();
    }

    static void FrameLoop() {
        IntPtr timer = IntPtr.Zero;
        try {
            try { timer = CreateWaitableTimerEx(IntPtr.Zero, null, 2, 0x100002); } catch (EntryPointNotFoundException) { }
            while (!shuttingDown) {
                wake.WaitOne();
                while (running && !shuttingDown) {
                    long due = -(long)(frameMilliseconds * 10000);
                    if (timer != IntPtr.Zero && SetWaitableTimer(timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false)) WaitForSingleObject(timer, 1000);
                    else Thread.Sleep(8);
                    if (!running || shuttingDown || Interlocked.CompareExchange(ref framePosted, 1, 0) != 0) continue;
                    try { context.Post(delegate { try { if (!shuttingDown) Tick(); } finally { Interlocked.Exchange(ref framePosted, 0); } }, null); }
                    catch { Interlocked.Exchange(ref framePosted, 0); running = false; }
                }
            }
        } finally { if (timer != IntPtr.Zero) CloseHandle(timer); }
    }

    // Mouse messages only request a frame. Painting is coalesced and cannot be starved by mouse input.
    public static void Repaint(Control control) {
        if (control.IsDisposed || !control.IsHandleCreated) return;
        repaints.Add(control);
        Start();
    }

    public const double Fast = 140, Normal = 220;

    // Starts (or restarts) the animation identified by key; step receives an eased 0..1 value.
    public static void Run(object key, double ms, Action<float> step, Action done = null) {
        tweens.RemoveAll(t => Equals(t.Key, key));
        tweens.Add(new Tween { Key = key, Start = clock.ElapsedMilliseconds, Ms = ms, Step = step, Done = done });
        Start();
    }

    // Animates a float from its current value to target.
    public static void To(object key, float from, float target, double ms, Action<float> set, Action done = null) {
        Stop(key); // A zero-distance update must cancel an older tween with the same key too.
        if (Math.Abs(from - target) < 0.001f) { set(target); if (done != null) done(); return; }
        Run(key, ms, e => set(from + (target - from) * e), done);
    }

    public static float EaseOut(float t) { return 1 - (1 - t) * (1 - t) * (1 - t); }

    class Follower { public object Key; public float Value; public long Last; public Func<float> Target; public Action<float> Set; }
    static readonly List<Follower> followers = new List<Follower>();

    // Glides a value toward a target that may keep moving (wheel scrolling): new input retargets
    // the same glide instead of restarting a curve, so fast wheel turns stay continuous.
    public static void Follow(object key, float from, Func<float> target, Action<float> set) {
        var f = followers.FirstOrDefault(x => Equals(x.Key, key));
        if (f == null) followers.Add(f = new Follower { Key = key, Value = from, Last = clock.ElapsedMilliseconds });
        f.Target = target;
        f.Set = set;
        Start();
    }

    public static void Stop(object key) {
        tweens.RemoveAll(t => Equals(t.Key, key));
        followers.RemoveAll(f => Equals(f.Key, key));
    }

    static void Tick() {
        long now = clock.ElapsedMilliseconds;
        foreach (var f in followers.ToList()) {
            if (!followers.Contains(f)) continue;
            float goal = f.Target();
            f.Value += (goal - f.Value) * (float)(1 - Math.Exp(-(now - f.Last) / 70.0));
            f.Last = now;
            if (Math.Abs(goal - f.Value) < 0.5f) { f.Value = goal; followers.Remove(f); }
            try { f.Set(f.Value); } catch { }
        }
        foreach (var t in tweens.ToList()) {
            if (!tweens.Contains(t)) continue;
            float p = (float)Math.Min(1, (now - t.Start) / t.Ms);
            try { t.Step(EaseOut(p)); } catch { }
            if (p >= 1 && tweens.Contains(t)) {
                tweens.Remove(t);
                if (t.Done != null) try { t.Done(); } catch { }
            }
        }
        var dirty = repaints.ToArray();
        repaints.Clear();
        foreach (var control in dirty) if (!control.IsDisposed && control.IsHandleCreated) control.Update();
        if (tweens.Count == 0 && followers.Count == 0 && repaints.Count == 0) running = false;
    }
}
