using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Cover art from the local Steam library cache, or the game's own icon. Nothing is downloaded.
public static class Artwork {
    // The card background uses a representative colour from the original cover pixels.
    // LoadedTicks: when the art became available, so the grid can fade it in instead of popping.
    // Cover comes with its rounded corners already cut (premultiplied alpha), so the grid copies it as is
    // every frame, without clipping, hover filters or resampling.
    public class Art { public Image Cover; public Image Icon; public Color Tint = Theme.Accent; public long LoadedTicks = DateTime.UtcNow.Ticks; }

    public static int Radius { get { return Theme.S(10); } }
    public static int CardMargin { get { return Theme.S(8); } }

    static readonly Dictionary<string, Art> cache = new Dictionary<string, Art>();
    static string libraryCache;

    static string LibraryCache {
        get {
            if (libraryCache == null) {
                libraryCache = "";
                using (var k = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam")) {
                    string root = k == null ? null : Convert.ToString(k.GetValue("SteamPath"));
                    if (!String.IsNullOrEmpty(root)) libraryCache = Path.Combine(root, "appcache", "librarycache");
                }
            }
            return libraryCache;
        }
    }

    public static Art Get(string key) {
        lock (cache) {
            Art a;
            return cache.TryGetValue(key, out a) ? a : null;
        }
    }

    public static void Clear() {
        lock (cache) {
            foreach (var a in cache.Values) {
                if (a.Cover != null) a.Cover.Dispose();
                if (a.Icon != null) a.Icon.Dispose();
            }
            cache.Clear();
        }
    }

    // Loads art for every game in the background, resized to the card size so only small bitmaps stay in memory.
    public static Task Load(IEnumerable<LibraryGame> games, Size cover, Action loaded) {
        var pending = games.Where(g => Get(g.Key) == null).ToList();
        return Task.Factory.StartNew(() => {
            foreach (var g in pending) {
                var art = new Art();
                try {
                    string file = SteamCover(g.Key);
                    if (file != null) art.Cover = Fit(file, cover);
                    if (art.Cover == null) {
                        string exe = g.Rules.Select(r => r.Exe).FirstOrDefault(File.Exists);
                        if (exe != null) art.Icon = Launchers.ExtractIcon(exe, Theme.S(64));
                    }
                    var source = art.Cover ?? art.Icon;
                    if (source != null) art.Tint = Tint(source);
                } catch { }
                art.LoadedTicks = DateTime.UtcNow.Ticks;
                lock (cache) cache[g.Key] = art;
                loaded();
            }
        });
    }

    static string SteamCover(string key) {
        if (!key.StartsWith("steam:", StringComparison.OrdinalIgnoreCase) || LibraryCache == "" || !Directory.Exists(LibraryCache)) return null;
        string id = key.Substring(6);
        uint appId;
        if (!UInt32.TryParse(id, out appId) || appId == 0) return null;
        string folder = Path.Combine(LibraryCache, id);
        // Steam uses both names for portrait covers, including hashed cache folders.
        foreach (string name in new[] { "library_600x900.jpg", "library_600x900.png", "library_capsule.jpg", "library_capsule.png" }) {
            if (Directory.Exists(folder)) {
                string found = Directory.EnumerateFiles(folder, name, SearchOption.AllDirectories).FirstOrDefault();
                if (found != null) return found;
            }
            string legacy = Path.Combine(LibraryCache, id + "_" + name);
            if (File.Exists(legacy)) return legacy;
        }
        return null;
    }

    // Pick a representative cluster of real pixels, without inventing saturation or brightness.
    static Color Tint(Image image) {
        var pixels=image as Bitmap;bool owns=pixels==null;
        if(owns)pixels=new Bitmap(image);
        try {
            var buckets = new Dictionary<int,List<Color>>();
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++) {
                    // Sample the original pixels: resizing can change even a solid cover's RGB values.
                    var c = pixels.GetPixel(Math.Min(pixels.Width-1,(2*x+1)*pixels.Width/16),Math.Min(pixels.Height-1,(2*y+1)*pixels.Height/16));
                    if(c.A<128||c.GetBrightness()<.08f)continue;
                    int key=(c.R/32)<<6|(c.G/32)<<3|c.B/32;List<Color> group;
                    if(!buckets.TryGetValue(key,out group))buckets[key]=group=new List<Color>();group.Add(c);
                }
            if(buckets.Count==0)return Theme.Muted;
            var dominant=buckets.Values.OrderByDescending(group=>Math.Pow(group.Count,.65)*(.2+group.Average(c=>c.GetSaturation()))*(.25+Math.Sqrt(group.Average(c=>c.GetBrightness())))).First();
            return Color.FromArgb((int)dominant.Average(c=>c.R),(int)dominant.Average(c=>c.G),(int)dominant.Average(c=>c.B));
        } finally {if(owns)pixels.Dispose();}
    }

    static Image Fit(string file, Size size) {
        if (new FileInfo(file).Length > 10 * 1024 * 1024) return null; // cover art is ~100 KB; ignore anything odd
        using (var stream = File.OpenRead(file)) using (var src = Image.FromStream(stream, false, true))
        using (var scaled = new Bitmap(size.Width, size.Height)) {
            using (var g = Graphics.FromImage(scaled)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float s = Math.Max((float)size.Width / src.Width, (float)size.Height / src.Height);
                float w = src.Width * s, h = src.Height * s;
                g.DrawImage(src, (size.Width - w) / 2, (size.Height - h) / 2, w, h);
            }
            // Cut the rounded corners once, anti-aliased, into the fast premultiplied format.
            var bmp = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp)) using (var brush = new TextureBrush(scaled)) using (var path = Theme.Round(new RectangleF(0, 0, size.Width, size.Height), Radius)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.FillPath(brush, path);
            }
            return bmp;
        }
    }

    // A bounded coloured surface behind cover + label. No cover bitmap is blurred or tinted.
    public static void DrawCardBackground(Graphics g,Rectangle card,Color tint,float amount){
        amount=Math.Max(0,Math.Min(1,amount));if(amount<=0)return;
        var frame=Rectangle.Inflate(card,Theme.S(6),Theme.S(6));
        using(var path=Theme.Round(frame,Radius+Theme.S(4)))
        using(var fill=new LinearGradientBrush(frame,Color.FromArgb((int)(155*amount),tint),Color.FromArgb((int)(85*amount),tint),90f))
        using(var edge=new Pen(Color.FromArgb((int)(115*amount),tint),Theme.S(1))){g.FillPath(fill,path);g.DrawPath(edge,path);}
    }
}
