using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Menu;
internal static class CharacterSelectionPanelTests
{
    [ModuleInitializer] internal static void Run()
    {
        try
        {
            _=new TestContentManager();using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
            using var canvas=new Canvas(skin){Size=new Point(1024,768)};
            var window=new SelectCharacterWindow(canvas,null!);
            const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(SelectCharacterWindow).GetMethod("EnsureInitialized",hidden)!.Invoke(window,null);
            var pane=(Base)typeof(SelectCharacterWindow).GetField("_characterDetailsPanel",hidden)!.GetValue(window)!;
            foreach(var name in new[]{"_infoLabel","_nameLabel","_levelCaptionLabel","_levelValueLabel","_classCaptionLabel","_classValueLabel","_guildCaptionLabel","_guildValueLabel"})
            {
                var label=(Label)typeof(SelectCharacterWindow).GetField(name,hidden)!.GetValue(window)!;
                if(label.Parent!=pane || label.X<16 || label.Y<16 || label.Right>pane.Width-16 || label.Bottom>pane.Height-16)throw new Exception("Character data must stay inside its separate bordered pane: "+name);
            }
            if(pane.Width!=360 || pane.Height!=142 || pane.MouseInputEnabled)throw new Exception("Character details pane must match the reference and pass input through");
            var portrait=(Base)typeof(SelectCharacterWindow).GetField("_previewContainer",hidden)!.GetValue(window)!;
            if(portrait.Parent==pane)throw new Exception("Portrait must remain beside the details pane");
            System.Console.WriteLine("PASS character selection: separate framed data pane, eight contained labels, independent portrait and preserved controls");
        }
        catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}
    }
}
