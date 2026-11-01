using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

public static class SecurityChecks {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Rejected(Action read,string message){bool rejected=false;try{read();}catch(CryptographicException){rejected=true;}Check(rejected,message);}
    public static void Main(string[] args){
        try{
            if(args[0]=="--read-recovery"){
                var recovery=Store.Read<Recovery>(args[1]);Check(recovery.Device=="fixture"&&recovery.OwnerPid==4242,"Recovery read failed in helper process.");File.WriteAllText(args[2],"PASS");return;
            }
            Run(args[0]);
        }catch(Exception e){File.WriteAllText(args[args.Length-1]+".error",e.ToString());Environment.ExitCode=1;}
    }
    static void Run(string report){
        string temp=Path.GetFullPath(Path.GetTempPath());
        string root=Path.GetFullPath(Path.Combine(temp,"ResMode-security-"+Guid.NewGuid().ToString("N")));
        string previousRoot=Store.Root;var results=new List<string>();Directory.CreateDirectory(root);
        try{
            Store.Root=root;string file=Store.Config;
            var config=new Settings{Width=2880,Height=1620};config.Games.Add(new GameRule{Name="Private fixture",Exe=@"C:\PrivateGames\Fixture\game.exe",Source="Manual"});
            using(var stream=File.Create(file))new XmlSerializer(typeof(Settings)).Serialize(stream,config);
            Check(Store.LoadSettings().Games[0].Name=="Private fixture","Legacy settings were not preserved.");
            Store.SaveSettings(config);byte[] saved=File.ReadAllBytes(file);
            Check(!Encoding.UTF8.GetString(saved).Contains("PrivateGames")&&!Encoding.UTF8.GetString(saved).Contains("Private fixture"),"Private settings written in plaintext.");
            Check(Store.LoadSettings().Width==2880&&Store.LoadSettings().Games[0].Exe==config.Games[0].Exe,"Encrypted settings round trip failed.");
            Store.SaveSettings(config);Check(!saved.SequenceEqual(File.ReadAllBytes(file)),"Encryption reused identical ciphertext.");
            byte[] damaged=File.ReadAllBytes(file);damaged[damaged.Length-1]^=1;File.WriteAllBytes(file,damaged);
            Rejected(()=>Store.LoadSettings(),"Tampered encrypted settings were accepted.");
            Check(File.ReadAllBytes(file).SequenceEqual(damaged),"Corrupt settings were overwritten while reading.");Store.SaveSettings(config);
            string different=Path.Combine(root,"other.xml");File.Copy(file,different);Rejected(()=>Store.Read<Settings>(different),"Encrypted files were accepted under a different purpose.");
            results.Add("PASS: legacy settings, per-user DPAPI encryption, randomized ciphertext, integrity and file-purpose binding.");

            string xml=Path.Combine(root,"unsafe.xml");File.WriteAllText(xml,"<!DOCTYPE Settings [<!ENTITY x SYSTEM 'file:///not-a-real-private-file'>]><Settings><Width>&x;</Width></Settings>");
            bool refused=false;try{Store.Read<Settings>(xml);}catch(InvalidOperationException){refused=true;}Check(refused,"XML external entities were accepted.");
            Store.Write(Store.State,new Recovery{Device="fixture",OwnerPid=4242,Width=1920,Height=1080});
            string childReport=Path.Combine(root,"child.txt");
            using(var process=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--read-recovery \""+Store.State+"\" \""+childReport+"\""){UseShellExecute=false,CreateNoWindow=true})){
                if(!process.WaitForExit(15000)){process.Kill();throw new Exception("Recovery helper timeout.");}Check(process.ExitCode==0&&File.Exists(childReport),"A same-user helper could not read protected recovery state.");
            }
            results.Add("PASS: XML entities blocked and recovery read by a separate helper process without changing displays.");

            string legacy="2026-11-01T12:00:00 [sistema] Private legacy history";
            File.WriteAllText(Store.LogFile,legacy+Environment.NewLine,Encoding.UTF8);
            Store.Log("Private path C:\\PrivateGames\\Fixture\r\nforged entry");
            string disk=File.ReadAllText(Store.LogFile,Encoding.UTF8);
            Check(!disk.Contains("Private")&&!disk.Contains("PrivateGames"),"Legacy or new history leaked plaintext.");
            var lines=Store.ReadLogLines(Store.LogFile).ToArray();Check(lines.Length==2&&lines[0]==legacy&&lines[1].Contains("forged entry"),"History migration or newline sanitization failed.");
            Check(Directory.GetFiles(root,"*.tmp").Length==0,"Atomic save left temporary plaintext files.");
            results.Add("PASS: legacy history migration, protected append, readable history and no extra forged lines or leftover temporary files.");
            File.WriteAllLines(report,results);
        }finally{
            Store.Root=previousRoot;
            if(root.StartsWith(temp.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("ResMode-security-",StringComparison.Ordinal))Directory.Delete(root,true);
        }
    }
}
