using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

// Tells which company made the graphics card and opens that company's control panel, where DSR (NVIDIA) or
// Virtual Super Resolution (AMD) is switched on. It only starts a program the person already has: nothing is
// downloaded or installed, and no digital signature is involved.
public static class GpuHelper {
    public enum Maker { Unknown, Nvidia, Amd, Intel }

    // The display adapters Windows lists (device class {4d36e968-e325-11ce-bfc1-08002be10318}), by driver description and provider.
    public static Maker Detect() {
        bool nvidia = false, amd = false, intel = false;
        try {
            using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}")) {
                if (cls != null)
                    foreach (string name in cls.GetSubKeyNames()) {
                        if (!Regex.IsMatch(name, @"^\d{4}$")) continue;
                        using (var k = cls.OpenSubKey(name)) {
                            if (k == null) continue;
                            string text = (Convert.ToString(k.GetValue("DriverDesc")) + " " + Convert.ToString(k.GetValue("ProviderName"))).ToLowerInvariant();
                            if (text.Contains("nvidia")) nvidia = true;
                            else if (text.Contains("amd") || text.Contains("radeon") || text.Contains("advanced micro devices")) amd = true;
                            else if (text.Contains("intel")) intel = true;
                        }
                    }
            }
        } catch { }
        return nvidia ? Maker.Nvidia : amd ? Maker.Amd : intel ? Maker.Intel : Maker.Unknown;
    }

    public static string Name(Maker maker) {
        return maker == Maker.Nvidia ? "NVIDIA" : maker == Maker.Amd ? "AMD" : maker == Maker.Intel ? "Intel" : "placa de vídeo";
    }

    public static string ButtonText(Maker maker) {
        return maker == Maker.Nvidia ? "Abrir o painel da NVIDIA" : maker == Maker.Amd ? "Abrir o AMD Software" : "Abrir o painel da placa";
    }

    static string First(params string[] paths) {
        foreach (string p in paths) if (!String.IsNullOrEmpty(p) && File.Exists(p)) return p;
        return null;
    }

    static bool Run(string file, string args = "") {
        try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true }); return true; } catch { return false; }
    }

    // Store apps register under the user's AppModel repository; the key name carries the package family.
    static string StoreApp(string namePrefix, string appId) {
        try {
            using (var packages = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
                if (packages != null)
                    foreach (string full in packages.GetSubKeyNames())
                        if (full.StartsWith(namePrefix + "_", StringComparison.OrdinalIgnoreCase)) {
                            string[] parts = full.Split('_'); // name_version_arch__publisherId
                            return "shell:AppsFolder\\" + namePrefix + "_" + parts[parts.Length - 1] + "!" + appId;
                        }
        } catch { }
        return null;
    }

    // Opens the maker's control panel if it is installed; otherwise the page to get it. The message says what happened.
    public static bool OpenPanel(Maker maker, out string message) {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (maker == Maker.Nvidia) {
            string store = StoreApp("NVIDIACorp.NVIDIAControlPanel", "NVIDIACorp.NVIDIAControlPanel");
            string classic = First(Path.Combine(pf, @"NVIDIA Corporation\Control Panel Client\nvcplui.exe"), Path.Combine(pf86, @"NVIDIA Corporation\Control Panel Client\nvcplui.exe"));
            string app = First(Path.Combine(pf, @"NVIDIA Corporation\NVIDIA app\CEF\NVIDIA app.exe"));
            if (store != null && Run("explorer.exe", store)) { message = "Painel de Controle NVIDIA aberto. Siga os passos e volte aqui."; return true; }
            if (classic != null && Run(classic)) { message = "Painel de Controle NVIDIA aberto. Siga os passos e volte aqui."; return true; }
            if (app != null && Run(app)) { message = "NVIDIA app aberto. Em Gráficos › Configurações globais, ache o DSR."; return true; }
            Run("https://www.nvidia.com/pt-br/software/nvidia-app/");
            message = "Não achei o painel da NVIDIA instalado. Abri a página para baixar o NVIDIA app.";
            return false;
        }
        if (maker == Maker.Amd) {
            string radeon = First(Path.Combine(pf, @"AMD\CNext\CNext\RadeonSoftware.exe"), Path.Combine(pf86, @"AMD\CNext\CNext\RadeonSoftware.exe"));
            if (radeon != null && Run(radeon)) { message = "AMD Software aberto. Vá em Configurações › Tela e siga os passos."; return true; }
            Run("https://www.amd.com/pt/support/download/drivers.html");
            message = "Não achei o AMD Software instalado. Abri a página de drivers da AMD.";
            return false;
        }
        message = maker == Maker.Intel
            ? "A Intel não oferece resolução virtual no Windows. Dá para criar uma resolução personalizada no Intel Graphics Command Center."
            : "Não consegui identificar a placa de vídeo. Procure o painel dela no menu Iniciar.";
        return false;
    }
}
