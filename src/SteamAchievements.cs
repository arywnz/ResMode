using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Win32;

// Read-only synchronization with Steam's own per-account library cache. No credentials or network calls.
public static class SteamAchievements {
    public class Progress {
        public bool Available;public int Total,Unlocked;public DateTime Updated;
        public string Message="Dados ainda não disponíveis na Steam";
    }
    sealed class Cached {public string Stamp;public Progress Value;}
    static readonly object gate=new object();
    static readonly Dictionary<string,Cached> cache=new Dictionary<string,Cached>(StringComparer.OrdinalIgnoreCase);

    public static Progress Get(string gameKey){
        try{
            uint app;
            if(gameKey==null||!gameKey.StartsWith("steam:",StringComparison.OrdinalIgnoreCase)||!UInt32.TryParse(gameKey.Substring(6),out app)||app==0)return new Progress{Message="Disponível para jogos da Steam"};
            string root=Launchers.RegString(Registry.CurrentUser,@"Software\Valve\Steam","SteamPath");
            if(String.IsNullOrEmpty(root))return new Progress{Message="Steam não encontrada"};
            string profile=null;
            using(var active=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess")){
                if(active!=null){long user=Convert.ToInt64(active.GetValue("ActiveUser",0));if(user>0)profile=Path.Combine(root,"userdata",user.ToString());}
            }
            if(profile==null){string users=Path.Combine(root,"userdata");if(Directory.Exists(users))profile=Directory.GetDirectories(users).Where(p=>File.Exists(Path.Combine(p,"config","localconfig.vdf"))).OrderByDescending(p=>File.GetLastWriteTimeUtc(Path.Combine(p,"config","localconfig.vdf"))).FirstOrDefault();}
            if(profile==null)return new Progress{Message="Abra sua biblioteca na Steam para sincronizar"};
            string file=Path.Combine(profile,"config","librarycache",app+".json");
            if(!File.Exists(file))return new Progress{Message="Abra este jogo na biblioteca da Steam"};
            var info=new FileInfo(file);if(info.Length>4*1024*1024)return new Progress();
            string stamp=info.LastWriteTimeUtc.Ticks+":"+info.Length;Cached previous;
            lock(gate)if(cache.TryGetValue(file,out previous)&&previous.Stamp==stamp)return previous.Value;
            string json;using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))json=reader.ReadToEnd();
            var value=Parse(json);value.Updated=info.LastWriteTime;
            var after=new FileInfo(file);if(after.LastWriteTimeUtc.Ticks+":"+after.Length!=stamp)return new Progress{Message="Aguardando atualização da Steam"};
            lock(gate){if(cache.Count>250)cache.Clear();cache[file]=new Cached{Stamp=stamp,Value=value};}return value;
        }catch{return new Progress{Message="Não foi possível ler os dados da Steam"};}
    }
    public static Progress Parse(string json){
        var serializer=new JavaScriptSerializer{MaxJsonLength=4*1024*1024,RecursionLimit=40};
        var pairs=serializer.DeserializeObject(json) as object[];if(pairs==null)return new Progress();
        foreach(var entry in pairs){
            var pair=entry as object[];if(pair==null||pair.Length<2||!String.Equals(Convert.ToString(pair[0]),"achievements",StringComparison.Ordinal))continue;
            var wrapper=pair[1] as Dictionary<string,object>;object raw;
            if(wrapper==null||!wrapper.TryGetValue("data",out raw))continue;
            var data=raw as Dictionary<string,object>;object total,unlocked;
            if(data==null||!data.TryGetValue("nTotal",out total)||!data.TryGetValue("nAchieved",out unlocked))continue;
            int all=Convert.ToInt32(total),done=Convert.ToInt32(unlocked);if(all<0||done<0||done>all)return new Progress();
            return new Progress{Available=true,Total=all,Unlocked=done,Message=all==0?"Este jogo não tem conquistas na Steam":"Dados locais da Steam"};
        }return new Progress();
    }
}
