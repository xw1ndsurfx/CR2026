using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Metalworking;
internal static class CombatPvpMetalworkEditorTests
{
    internal static void Run()
    {
        using(var pvp=new FrmPvpConfiguration("{}"))
        {
            var tabs=pvp.Controls.OfType<TabControl>().Single();var grid=tabs.TabPages[1].Controls.OfType<DataGridView>().Single();
            if(((System.Collections.IList)grid.DataSource!).Count!=10 || grid.AllowUserToDeleteRows || tabs.TabPages.Count!=4)throw new Exception("PvP editor must preserve exactly ten grades and separate ranked map/arena controls");
        }
        foreach(var kind in new[]{Intersect.Framework.Core.GameObjects.Events.Commands.MiniGameType.Smelting,Intersect.Framework.Core.GameObjects.Events.Commands.MiniGameType.Smithing})
        {
            var command=new Intersect.Framework.Core.GameObjects.Events.Commands.StartMiniGameCommand {Game=kind,MaxPlayers=1,DealerPlays=false,NpcPlayers=0};
            if(!command.HasValidSettings())throw new Exception("Foundry/forge access events must validate");
            using var dialog=new Intersect.Editor.Forms.Editors.Events.MiniGameCommandDialog(command);
            var summary=All(dialog).OfType<Label>().First(l=>l.Name=="ConfigurationSummary");
            if(!summary.Text.Contains("workshop"))throw new Exception("Workshop settings must not describe an alchemy board");
        }
        using(var forge=new FrmMetalworking("{}",MetalworkKind.Smithing))
        {
            var controls=All(forge).ToArray();if(controls.OfType<ComboBox>().Count(c=>c.DropDownStyle==ComboBoxStyle.DropDownList)<6 || !controls.OfType<DataGridView>().Any())throw new Exception("Forge editor needs item/material/profession/sound pickers");
        }
        using(var playlists=new FrmCombatPlaylists([]))if(!All(playlists).OfType<CheckedListBox>().Any())throw new Exception("Combat playlist editor needs map and music selection");
        using(var menu=new FrmMenuAnimations())if(!All(menu).OfType<DataGridView>().Single().Columns.Cast<DataGridViewColumn>().Any(c=>c.DataPropertyName=="TransparencyPercent"))throw new Exception("Menu animation editor needs opacity selection");
        System.Console.WriteLine("PASS PvP ten-grade editor and map/music, forge materials/sounds, main menu animation settings");
    }
    private static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in All(c))yield return child;}}
}
