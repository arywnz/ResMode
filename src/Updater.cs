using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

// A newer release published on GitHub. Url and Sha256 are set only when the release carries the MSI with
// the checksum GitHub computed for it; without them the person is sent to the release page instead.
public class UpdateOffer {
    public string Tag, Version, Page, Url, Sha256;
    public long Size;
    public bool CanDownload { get { return Url != null && Sha256 != null; } }
}

// Looks for a newer release on GitHub, downloads its MSI, checks the checksum and hands over to msiexec.
// The only request it makes is a plain GET to api.github.com (and the download from github.com); it sends
// nothing about the PC besides the program name and version in the User-Agent.
public static class Updater {
    public const string Repository = "arywnz/ResMode";
    const string Api = "https://api.github.com/repos/" + Repository + "/releases?per_page=15";
    const string PagePrefix = "https://github.com/" + Repository + "/releases/";
    const long MaxSize = 80L * 1024 * 1024;
    static readonly Regex TagPattern = new Regex(@"^v(\d{1,5})\.(\d{1,5})\.(\d{1,5})(?:-([0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*))?$");
    static readonly Regex DigestPattern = new Regex(@"^sha256:([0-9a-f]{64})$");

    class Client : WebClient {
        protected override WebRequest GetWebRequest(Uri address) {
            var request = base.GetWebRequest(address);
            request.Timeout = 20000;
            var http = request as HttpWebRequest;
            if (http != null) http.ReadWriteTimeout = 20000;
            return request;
        }
    }

    static Client NewClient() {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var web = new Client();
        web.Headers[HttpRequestHeader.UserAgent] = AppInfo.Name + "/" + AppInfo.Release;
        web.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
        return web;
    }

    // Semantic-version order: 0.2.2-alpha.1 < 0.2.2-alpha.2 < 0.2.2 < 0.2.3. Both strings are versions without the "v".
    public static int Compare(string a, string b) {
        var x = TagPattern.Match("v" + a);
        var y = TagPattern.Match("v" + b);
        if (!x.Success || !y.Success) throw new FormatException("Versão inválida.");
        for (int i = 1; i <= 3; i++) {
            int c = Int32.Parse(x.Groups[i].Value).CompareTo(Int32.Parse(y.Groups[i].Value));
            if (c != 0) return c;
        }
        string pa = x.Groups[4].Value, pb = y.Groups[4].Value;
        if (pa == pb) return 0;
        if (pa == "") return 1; // a release comes after its own prereleases
        if (pb == "") return -1;
        string[] sa = pa.Split('.'), sb = pb.Split('.');
        for (int i = 0; i < Math.Min(sa.Length, sb.Length); i++) {
            int na, nb;
            bool ia = Int32.TryParse(sa[i], out na), ib = Int32.TryParse(sb[i], out nb);
            int c = ia && ib ? na.CompareTo(nb) : ia ? -1 : ib ? 1 : String.CompareOrdinal(sa[i], sb[i]);
            if (c != 0) return c;
        }
        return sa.Length.CompareTo(sb.Length);
    }

    // Blocking: run it on a worker thread. Null means this PC already has the newest release.
    // The list is asked for with its last ETag: when nothing new was published GitHub answers 304 with no body
    // (and no cost against the rate limit), which is what makes checking every few minutes cheap.
    static string lastTag, lastJson;

    public static UpdateOffer Check() {
        string json;
        using (var web = NewClient()) {
            if (lastTag != null && lastJson != null) web.Headers[HttpRequestHeader.IfNoneMatch] = lastTag;
            try {
                json = web.DownloadString(Api);
                lastTag = web.ResponseHeaders[HttpResponseHeader.ETag];
                lastJson = json;
            } catch (WebException e) {
                var response = e.Response as HttpWebResponse;
                if (response == null || response.StatusCode != HttpStatusCode.NotModified || lastJson == null) throw;
                json = lastJson;
            }
        }
        return Pick(json);
    }

    // The newest release above this version that is not a draft; kept apart from the download so it can be tested.
    public static UpdateOffer Pick(string json) {
        var parser = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        var releases = parser.Deserialize<ArrayList>(json);
        UpdateOffer best = null;
        foreach (object item in releases) {
            var release = item as Dictionary<string, object>;
            if (release == null || Text(release, "draft") == "True") continue;
            string tag = Text(release, "tag_name");
            var match = TagPattern.Match(tag);
            if (!match.Success) continue;
            string version = tag.Substring(1);
            if (Compare(version, AppInfo.Release) <= 0) continue;
            if (best != null && Compare(version, best.Version) <= 0) continue;
            best = Offer(release, tag, version);
        }
        return best;
    }

    static UpdateOffer Offer(Dictionary<string, object> release, string tag, string version) {
        var offer = new UpdateOffer { Tag = tag, Version = version, Page = PagePrefix + "tag/" + tag };
        string page = Text(release, "html_url");
        if (page.StartsWith(PagePrefix, StringComparison.Ordinal)) offer.Page = page;
        var assets = release.ContainsKey("assets") ? release["assets"] as ArrayList : null;
        if (assets == null) return offer;
        string wanted = AppInfo.Name + "-" + version + "-win-x64.msi";
        foreach (object entry in assets) {
            var asset = entry as Dictionary<string, object>;
            if (asset == null || Text(asset, "name") != wanted) continue;
            string url = Text(asset, "browser_download_url");
            long size;
            var digest = DigestPattern.Match(Text(asset, "digest"));
            if (!url.StartsWith("https://github.com/" + Repository + "/releases/download/" + tag + "/", StringComparison.Ordinal)) continue;
            if (!Int64.TryParse(Text(asset, "size"), out size) || size <= 0 || size > MaxSize) continue;
            if (!digest.Success) continue;
            offer.Url = url;
            offer.Size = size;
            offer.Sha256 = digest.Groups[1].Value;
            break;
        }
        return offer;
    }

    static string Text(Dictionary<string, object> map, string key) {
        object value;
        return map.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : "";
    }

    static string Folder { get { return Path.Combine(Store.Root, "update"); } }

    // Blocking. Returns the path of a verified MSI; a file whose checksum differs is deleted and never run.
    // The checksum comes from the same GitHub release, so it catches damaged or truncated downloads;
    // it is not a substitute for a code signature.
    public static string Download(UpdateOffer offer) {
        if (!offer.CanDownload) throw new InvalidOperationException("Esta versão não traz o instalador com checksum.");
        Directory.CreateDirectory(Folder);
        foreach (string old in Directory.GetFiles(Folder)) { try { File.Delete(old); } catch { } }
        string file = Path.Combine(Folder, AppInfo.Name + "-" + offer.Version + "-win-x64.msi"), part = file + ".part";
        using (var web = NewClient()) web.DownloadFile(offer.Url, part);
        string actual;
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(part)) actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if (!String.Equals(actual, offer.Sha256, StringComparison.Ordinal)) {
            File.Delete(part);
            throw new IOException("O arquivo baixado não confere com o publicado no GitHub. Nada foi instalado.");
        }
        File.Move(part, file);
        return file;
    }

    // True when this copy was put in place by the MSI, so the MSI can replace it. A copy run from a ZIP or a
    // build folder is left alone; for those the person is sent to the release page.
    public static bool IsInstalled {
        get {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\ResMode\Installer")) {
                    string path = key == null ? null : key.GetValue("InstallPath") as string;
                    if (String.IsNullOrEmpty(path)) return false;
                    string here = Path.GetDirectoryName(Application.ExecutablePath);
                    return String.Equals(Path.GetFullPath(path).TrimEnd('\\'), here.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }
            } catch { return false; }
        }
    }

    // Starts a hidden helper that waits for this process (and its /guard companion) to exit, runs the MSI and
    // opens the app again. If the install fails, the old executable is still there and is started instead.
    public static void Install(string msi, int pid) {
        string exe = Application.ExecutablePath;
        string script =
            "$ErrorActionPreference='SilentlyContinue';" +
            "Wait-Process -Id " + pid + " -Timeout 30;" +
            "Get-Process -Name ResMode | Wait-Process -Timeout 20;" +
            "Start-Process -FilePath msiexec.exe -ArgumentList '/i','\"" + msi.Replace("'", "''") + "\"','/passive','/norestart' -Wait;" +
            "if(Test-Path -LiteralPath '" + exe.Replace("'", "''") + "'){Start-Process -FilePath '" + exe.Replace("'", "''") + "'}"; // no /background: the window opens again after the update
        string powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        Process.Start(new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) { UseShellExecute = false, CreateNoWindow = true });
    }

    public static void OpenPage(UpdateOffer offer) {
        if (offer.Page.StartsWith(PagePrefix, StringComparison.Ordinal)) Process.Start(new ProcessStartInfo(offer.Page) { UseShellExecute = true });
    }
}
