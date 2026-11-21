using Intersect.Client.Framework.Content;
using Intersect.Client.MonoGame.File_Management;
using NUnit.Framework;

namespace Intersect.Tests.Client;
[TestFixture]
public sealed class LocalResourceRefreshTests
{
    [Test]
    public void RescanFindsNewFilesUnderRenamedPathsAndPreservesLiveAssetsOnRepeatedClicks()
    {
        var root=Path.Combine(Path.GetTempPath(),"resource-refresh-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"sounds","Fishing"));
        try
        {
            var live=new Asset("live");var lookup=new Dictionary<string,IAsset>{{"live",live}};var opened=new List<string>();var failures=0;
            IAsset Create(string key,string path){opened.Add(path);return new Asset(key);}
            File.WriteAllText(Path.Combine(root,"sounds","Fishing","Splash.WAV"),"first");
            Assert.That(LocalResourceScanner.AddFiles(root,"sounds",".wav",lookup,Create,true,ref failures),Is.EqualTo(1));
            Assert.That(lookup.ContainsKey("fishing/splash"),Is.True);Assert.That(opened[0],Does.EndWith("Splash.WAV"));
            var first=lookup["fishing/splash"];
            File.WriteAllText(Path.Combine(root,"sounds","splash.wav"),"second");
            Assert.That(LocalResourceScanner.AddFiles(root,"sounds",".wav",lookup,Create,true,ref failures),Is.EqualTo(1));
            Assert.That(lookup["live"],Is.SameAs(live));Assert.That(lookup["fishing/splash"],Is.SameAs(first));
            Assert.That(LocalResourceScanner.AddFiles(root,"sounds",".wav",lookup,Create,true,ref failures),Is.Zero);
            Assert.That(opened,Has.Count.EqualTo(2));Assert.That(failures,Is.Zero);
        }
        finally{var target=Path.GetFullPath(root);var temporaryRoot=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!target.StartsWith(temporaryRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Test cleanup escaped the temporary directory");
            Directory.Delete(target,true);}
    }
    [Test]
    public void OneUnreadableResourceDoesNotHideOtherNewFilesOrExistingPackedEntries()
    {
        var root=Path.Combine(Path.GetTempPath(),"resource-refresh-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"items"));
        try
        {
            foreach(var name in new[]{"bad.png","new.png","packed.png","ignore.wav"})File.WriteAllText(Path.Combine(root,"items",name),"test");
            var packed=new Asset("packed");var lookup=new Dictionary<string,IAsset>{{"packed.png",packed}};var failures=0;
            var added=LocalResourceScanner.AddFiles(root,"items",".png",lookup,(key,path)=>key=="bad.png"?throw new IOException("copy in progress"):new Asset(key),false,ref failures);
            Assert.That(added,Is.EqualTo(1));Assert.That(failures,Is.EqualTo(1));Assert.That(lookup["packed.png"],Is.SameAs(packed));
            Assert.That(lookup.ContainsKey("new.png"),Is.True);Assert.That(lookup.ContainsKey("ignore.wav"),Is.False);
        }
        finally{var target=Path.GetFullPath(root);var temporaryRoot=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!target.StartsWith(temporaryRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Test cleanup escaped the temporary directory");
            Directory.Delete(target,true);}
    }
    private sealed class Asset(string id):IAsset
    {
        public string Id=>id;public string Name=>id;public bool IsLoaded=>false;public bool IsDisposed=>false;
        public event Action<IAsset>? Disposed{add{}remove{}}public event Action<IAsset>? Loaded{add{}remove{}}public event Action<IAsset>? Unloaded{add{}remove{}}
    }
}
