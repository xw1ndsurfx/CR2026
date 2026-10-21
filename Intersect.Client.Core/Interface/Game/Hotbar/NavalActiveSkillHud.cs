using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game.Hotbar;
internal sealed class NavalActiveSkillHud(Base parent) : Base(parent, nameof(NavalActiveSkillHud))
{
    private readonly Dictionary<NavalSkillKind, (Base Row, Label Caption, ImagePanel Icon)> _rows = [];
    internal void Update(ShipSnapshot? ship, bool suppressed)
    {
        MouseInputEnabled=KeyboardInputEnabled=false;
        var skills=ship?.ActiveSkills ?? [];
        IsHidden=suppressed || skills.Length==0;
        if (IsHidden) return;
        var width=Math.Min(300, Parent.Width-24);
        SetBounds(Math.Max(12, Parent.Width-width-300),Math.Max(72,Math.Min(365,Parent.Height-skills.Length*58-110)),width,skills.Length*58);
        foreach (var entry in _rows.Values) entry.Row.Hide();
        for (var i=0;i<skills.Length;i++)
        {
            var skill=skills[i];
            if (!_rows.TryGetValue(skill.Kind,out var row))
            {
                var panel=new Panel(this, skill.Kind.ToString()) {BackgroundColor=new Color(225,31,24,18),MouseInputEnabled=false};
                var icon=new ImagePanel(panel,"Icon") {MaintainAspectRatio=true,ShouldDrawBackground=false,MouseInputEnabled=false};icon.SetBounds(8,6,40,40);
                var caption=NavalManagementWindow.Text(panel,"Skill","",58,4,width-66,46,12);
                _rows[skill.Kind]=row=(panel,caption,icon);
            }
            row.Row.SetBounds(0,i*58,width,54);row.Row.Show();
            row.Caption.Text=NavalSetupText.Get(NavalSkillDefinition.Caption(skill.Kind),ClientLanguages.Current)+" - "+Math.Max(0,(skill.RemainingMilliseconds+999)/1000)+"s";
            row.Icon.Texture=GameContentManager.Current.GetTexture(TextureType.Spell,skill.Icon);
        }
    }
}
