using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Events.Commands;

internal static class RoyalCombatEditorTests
{
    internal static void Run()
    {
        using var editor = new FrmRoyalCombatConfiguration(new RoyalCombatConfiguration().ToJson(), true);
        var show = typeof(FrmRoyalCombatConfiguration).GetMethod("ShowMode", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var grid = Descendants(editor).OfType<PropertyGrid>().Single();
        var mastery = (MasteryCategory)grid.SelectedObject!;
        mastery.BaseExperience = 150;
        show.Invoke(editor, [false]); show.Invoke(editor, [true]);
        if (!ReferenceEquals(grid.SelectedObject, mastery) || mastery.BaseExperience != 150)
            throw new Exception("Switching royal editor menus discarded unsaved mastery changes.");
        var dialog = typeof(FrmRoyalCombatConfiguration).Assembly.GetType("Intersect.Editor.Forms.Editors.Events.MiniGameCommandDialog", true)!;
        var command = new StartMiniGameCommand { Game = MiniGameType.Archery };
        using var form = (Form)Activator.CreateInstance(dialog, [command, (MiniGameType?)MiniGameType.Archery])!;
        if (Descendants(form).Any(c => c.Name == "CurrencyStatus") || form.AcceptButton is not Button || form.CancelButton is not Button)
            throw new Exception("The archery NPC command exposed gambling controls or lost Save/Cancel.");
        if ((int)MiniGameType.Smithing != 7 || (int)MiniGameType.Archery != 8)
            throw new Exception("Existing mini-game serialization values changed.");
        System.Console.WriteLine("PASS: royal editor draft preservation, dedicated NPC command dialog and stable mini-game IDs.");
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
}
