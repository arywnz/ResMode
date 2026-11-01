using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

// Windows manages the per-user keys. No password, private certificate or shared key is distributed.
public static class LocalData {
    static readonly byte[] Magic=Encoding.ASCII.GetBytes("ResMode-DPAPI-1\n");
    const int Limit=8*1024*1024;
    const string LogPrefix="RML1:";
    static byte[] Entropy(string purpose){return Encoding.UTF8.GetBytes("ResMode.LocalData.v1:"+purpose);}
    static byte[] Seal(byte[] data,string purpose){return ProtectedData.Protect(data,Entropy(purpose),DataProtectionScope.CurrentUser);}
    static byte[] Open(byte[] data,string purpose){return ProtectedData.Unprotect(data,Entropy(purpose),DataProtectionScope.CurrentUser);}

    public static byte[] ReadXmlBytes(string file){
        byte[] bytes;
        using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
            if(stream.Length>Limit+65536)throw new InvalidDataException("Arquivo local grande demais.");
            using(var memory=new MemoryStream()){stream.CopyTo(memory);bytes=memory.ToArray();}
        }
        if(bytes.Take(Magic.Length).SequenceEqual(Magic))bytes=Open(bytes.Skip(Magic.Length).ToArray(),"xml:"+Path.GetFileName(file).ToLowerInvariant());
        if(bytes.Length>Limit)throw new InvalidDataException("Arquivo local grande demais.");
        return bytes; // Legacy XML is accepted; its next successful save is protected.
    }
    public static void WriteXmlBytes(string file,byte[] bytes){
        if(bytes.Length>Limit)throw new InvalidDataException("Arquivo local grande demais.");
        var sealedBytes=Seal(bytes,"xml:"+Path.GetFileName(file).ToLowerInvariant());
        AtomicWrite(file,Magic.Concat(sealedBytes).ToArray());
    }
    public static void AtomicWrite(string file,byte[] bytes){
        if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("O arquivo local não pode ser um link.");
        string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(file))File.Replace(temp,file,null);
            else try{File.Move(temp,file);}catch(IOException){if(!File.Exists(file))throw;File.Replace(temp,file,null);}
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static string ProtectLog(string line){return LogPrefix+Convert.ToBase64String(Seal(Encoding.UTF8.GetBytes(line),"log"));}
    public static string ReadLog(string line){
        if(line.Length>65536)throw new InvalidDataException("Registro local grande demais.");
        if(!line.StartsWith(LogPrefix,StringComparison.Ordinal))return line;
        return Encoding.UTF8.GetString(Open(Convert.FromBase64String(line.Substring(LogPrefix.Length)),"log"));
    }
    public static void MigrateLog(string file){
        if(new FileInfo(file).Length>2*1024*1024)throw new InvalidDataException("Histórico local grande demais.");
        var lines=File.ReadAllLines(file,Encoding.UTF8);
        if(lines.All(line=>line.StartsWith(LogPrefix,StringComparison.Ordinal)||line.Length==0))return;
        AtomicWrite(file,Encoding.UTF8.GetBytes(String.Join(Environment.NewLine,lines.Select(line=>line.Length==0||line.StartsWith(LogPrefix,StringComparison.Ordinal)?line:ProtectLog(line)))+Environment.NewLine));
    }
    public static void WithFileLock(string file,Action action){
        string key;using(var hash=SHA256.Create())key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(file).ToUpperInvariant()))).Replace("-","");
        using(var mutex=new Mutex(false,"Local\\ResMode.Data."+key)){
            bool taken=false;
            try{try{taken=mutex.WaitOne(5000);}catch(AbandonedMutexException){taken=true;}if(!taken)throw new IOException("Arquivo local ocupado.");action();}
            finally{if(taken)mutex.ReleaseMutex();}
        }
    }
}
