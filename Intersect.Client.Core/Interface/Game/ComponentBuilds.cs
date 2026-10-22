using Intersect.Framework.Core;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Localization;
namespace Intersect.Client.Interface.Game;
internal static class ComponentBuilds
{
    internal static string Server="";
    internal static Label Create(Base parent,string name)=>new(parent,name)
    {
        AutoSizeToContents=false,MouseInputEnabled=false,Font=GameContentManager.Current.GetFont("sourcesanspro")??parent.Skin.DefaultFont,
        FontSize=8,TextColorOverride=new Color(210,167,182,198),Text=ClientLanguages.Text("Client")+" "+BuildIdentity.Label
    };
    internal static void Update(Label label)=>label.Text=ClientLanguages.Text("Client")+" "+BuildIdentity.Label+
        (Server.Length==0?"":" | "+ClientLanguages.Text("Server")+" "+Server);
}
