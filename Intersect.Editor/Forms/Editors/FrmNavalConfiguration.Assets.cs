using System.Media;
using Intersect.Framework.Core.Naval;
namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmNavalConfiguration
{
    private SoundPlayer? _navalSoundPreview;
    private Control AssetField(string name,string current,Action<string> set)
    {
        var crewArt=name=="CrewCannonGraphic";var icon=name.EndsWith("Icon");var sound=name.EndsWith("Sound");
        var folder=icon?"spells":sound?"sounds":"ocean";
        var directory=Path.Combine("resources",folder);
        var panel=new Panel {Dock=DockStyle.Fill};
        var box=new ComboBox {Name=name,DropDownStyle=ComboBoxStyle.DropDownList,Anchor=AnchorStyles.Left|AnchorStyles.Right};
        var names=Directory.Exists(directory)?Directory.GetFiles(directory,sound?"*.wav":"*.png").Select(Path.GetFileName).Where(n=>NavalPresentation.SafeAsset(n)).Cast<string>().OrderBy(n=>n).ToList():[];
        if(crewArt) names=names.Where(n=>CompatibleCrewArtwork(Path.Combine(directory,n+".crew.json"))).ToList();
        else if(!icon && !sound) names=names.Where(n=>CompatibleEffect(Path.Combine(directory,n+".effects.json"),name)).ToList();
        names.Insert(0,"");if(!names.Contains(current)) names.Add(current);
        box.Items.AddRange(names.Cast<object>().ToArray());box.SelectedItem=current;
        PictureBox? preview=null;
        void ShowPreview()
        {
            if(preview==null) return;
            var old=preview.Image;preview.Image=null;old?.Dispose();
            if(NavalPresentation.SafeAsset(box.Text) && File.Exists(Path.Combine(directory,box.Text)))
            {
                try {using var file=File.OpenRead(Path.Combine(directory,box.Text));using var image=Image.FromStream(file);preview.Image=new Bitmap(image);}
                catch(Exception e) when(e is IOException or ArgumentException or OutOfMemoryException) { _status.Text=T("The selected resource is unavailable."); }
            }
        }
        if(icon || crewArt)
        {
            preview=new PictureBox {Name=name+"Preview",Dock=DockStyle.Right,Width=72,SizeMode=PictureBoxSizeMode.Zoom,BorderStyle=BorderStyle.FixedSingle,BackColor=System.Drawing.Color.FromArgb(25,35,50)};
            preview.Disposed+=(_,_)=>preview.Image?.Dispose();panel.Controls.Add(preview);
        }
        if(sound)
        {
            var listen=new Button {Text=T("Listen"),Dock=DockStyle.Right,Width=80};
            listen.Click+=(_,_)=>
            {
                if(!NavalPresentation.SafeAsset(box.Text) || string.IsNullOrWhiteSpace(box.Text)) return;
                try {_navalSoundPreview?.Stop();_navalSoundPreview?.Dispose();_navalSoundPreview=new SoundPlayer(Path.Combine(directory,box.Text));_navalSoundPreview.Play();}
                catch(Exception e) when(e is IOException or InvalidOperationException or TimeoutException) {_status.Text=T("The selected resource is unavailable.");}
            };
            panel.Controls.Add(listen);
        }
        panel.Controls.Add(box);box.BringToFront();
        void AlignSelector() => box.SetBounds(0,Math.Max(0,(panel.ClientSize.Height-box.Height)/2),Math.Max(30,panel.ClientSize.Width-(icon || crewArt?76:sound?84:0)),box.Height);
        panel.Resize+=(_,_)=>AlignSelector();AlignSelector();
        box.SelectedIndexChanged+=(_,_)=>{set(box.Text);ShowPreview();};ShowPreview();
        return panel;
    }
    private static bool CompatibleCrewArtwork(string path)
    {
        try
        {
            if(!File.Exists(path) || new FileInfo(path).Length>16384) return false;
            var art=Newtonsoft.Json.JsonConvert.DeserializeObject<CannonStationArtwork>(File.ReadAllText(path),new Newtonsoft.Json.JsonSerializerSettings {MaxDepth=8,TypeNameHandling=Newtonsoft.Json.TypeNameHandling.None});
            return art?.IsValid(art.ImageWidth,art.ImageHeight)==true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException) {return false;}
    }
    private static bool CompatibleEffect(string path,string property)
    {
        try
        {
            if(!File.Exists(path) || new FileInfo(path).Length>131072) return false;
            var atlas=Newtonsoft.Json.JsonConvert.DeserializeObject<NavalEffectAtlas>(File.ReadAllText(path));
            if(atlas?.IsValid(atlas.ImageWidth,atlas.ImageHeight)!=true) return false;
            if(property is "CannonProjectileGraphic" or "ProjectileGraphic") return Enumerable.Range(0,8).All(i=>atlas.Clips.ContainsKey("Projectile"+i));
            return atlas.Clips.ContainsKey(property=="CannonFireGraphic"?"Fire":property is "WaterImpactGraphic" or "WaterGraphic"?"WaterImpact":"HullImpact");
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException) {return false;}
    }
}
