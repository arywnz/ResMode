using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// Embedded, unmodified OFL fonts. Registration is private to this process, not a system install.
public static class LocalFonts {
    // Separate collections avoid GDI+ merging static weights with the same typographic family.
    static readonly List<PrivateFontCollection> collections = new List<PrivateFontCollection>();
    static readonly List<IntPtr> memory = new List<IntPtr>();
    static readonly List<IntPtr> registrations = new List<IntPtr>();
    static readonly object gate = new object();
    static readonly Dictionary<string,FontFamily> families = new Dictionary<string,FontFamily>(StringComparer.OrdinalIgnoreCase);
    static bool ready;
    [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data,uint size,IntPtr reserved,ref uint count);

    static void Load() {
        lock (gate) {
            if (ready) return;
            var assembly = typeof(LocalFonts).Assembly;
            foreach (string resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("ResMode.Fonts.", StringComparison.Ordinal) && n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))) {
                using (var stream = assembly.GetManifestResourceStream(resource)) using (var bytes = new MemoryStream()) {
                    stream.CopyTo(bytes);byte[] data = bytes.ToArray();
                    IntPtr pointer = Marshal.AllocHGlobal(data.Length);Marshal.Copy(data,0,pointer,data.Length);memory.Add(pointer);
                    var face=new PrivateFontCollection();face.AddMemoryFont(pointer,data.Length);collections.Add(face);
                    foreach(var family in face.Families)families[family.Name]=family;
                    uint count=0;
                    IntPtr handle=AddFontMemResourceEx(pointer,(uint)data.Length,IntPtr.Zero,ref count);if(handle!=IntPtr.Zero)registrations.Add(handle);
                }
            }
            ready = true;
        }
    }
    public static Font Create(float size,bool mono,bool emphasis,bool title=false) {
        Load();
        string name=mono?(emphasis?"Geist Mono Medium":"Geist Mono"):(title?"Geist SemiBold":emphasis?"Geist Medium":"Geist");
        FontFamily family;
        if(!families.TryGetValue(name,out family))throw new InvalidOperationException("Fonte local ausente: "+name+". Recompile com os recursos de assets/fonts.");
        return new Font(family,Math.Max(mono?9.5f:10f,size),FontStyle.Regular,GraphicsUnit.Point);
    }
}
