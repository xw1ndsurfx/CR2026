using Intersect.Client.Framework.Content;
using Intersect.Client.General;
using Intersect.Client.MonoGame.Audio;
using Intersect.Configuration;

namespace Intersect.Client.MonoGame.File_Management;
public partial class MonoContentManager
{
    public override ResourceRefreshResult RefreshLocalResources()
    {
        lock(Globals.GameLock)
        {
            var root=ClientConfiguration.ResourcesDirectory;
            var images=0;var failures=0;
            (string Folder,Dictionary<string,IAsset> Lookup)[] groups=[
                ("animations",mAnimationDict),("badges",mBadgeDict),("entities",mEntityDict),("ocean",mOceanDict),
                ("faces",mFaceDict),("fogs",mFogDict),("gui",mGuiDict),("images",mImageDict),
                ("achievements",mAchievementDict),("dungeons",mDungeonDict),("items",mItemDict),
                ("misc",mMiscDict),("paperdolls",mPaperdollDict),("resources",mResourceDict),("spells",mSpellDict),("tilesets",mTilesetDict)];
            foreach(var (folder,lookup) in groups)
                images+=LocalResourceScanner.AddFiles(root,folder,".png",lookup,
                    (key,path)=>Core.Graphics.Renderer.LoadTexture(Path.Combine(root,folder,key),path),false,ref failures);
            var sounds=LocalResourceScanner.AddFiles(root,"sounds",".wav",mSoundDict,
                (key,path)=>new MonoSoundSource(key,path),true,ref failures);
            sounds+=LocalResourceScanner.AddFiles(root,VoiceSoundFiles.Folder,".wav",mSoundDict,
                (key,path)=>new MonoSoundSource(key,path),true,ref failures,keyPrefix:VoiceSoundFiles.Folder+"/",recursive:false);
            var music=LocalResourceScanner.AddFiles(root,"music",".ogg",mMusicDict,
                (key,path)=>new MonoMusicSource(key,path),true,ref failures,recursive:false);
            return new(images,sounds,music,failures);
        }
    }
}
