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
        var originalDamage = mastery.Talents[0].DamagePercentPerRank;
        using (var talents = new FrmMasteryTalents(mastery))
        {
            talents.Result.Talents[0].DamagePercentPerRank = 2;
            if (mastery.Talents[0].DamagePercentPerRank != originalDamage ||
                Descendants(talents).OfType<ComboBox>().Count(c => c.Name.StartsWith("TalentRankSpell")) != 5)
                throw new Exception("Talent editing changed the source before Save or lost native rank-spell pickers.");
        }
        using var ammunition = new FrmWeaponAmmunition([]);
        if (ammunition.Result.Length != 0) throw new Exception("Opening ammunition compatibility invented a weapon mapping.");
        CheckRangeEditor();
        System.Console.WriteLine("PASS: royal editor draft preservation, dedicated NPC command dialog and stable mini-game IDs.");
    }
    private static void CheckRangeEditor()
    {
        var source = new ArcheryRules();
        using var editor = new FrmArcheryRangeRules(source) { ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-20000, -20000) };
        editor.Show(); Application.DoEvents();
        var preview = Descendants(editor).Single(c => c.Name == "ArcheryTargetPreview");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var frame = (System.Drawing.RectangleF)preview.GetType().GetProperty("Frame", flags)!.GetValue(preview)!;
        var x = (int)(frame.X + 190 * frame.Width / 320); var y = (int)(frame.Y + 84 * frame.Height / 180);
        preview.GetType().GetMethod("OnMouseDown", flags)!.Invoke(preview, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
        preview.GetType().GetMethod("OnMouseUp", flags)!.Invoke(preview, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
        if (Math.Abs(editor.Result.Rounds[0].Targets[0].X - 1.5) > .1 || source.Rounds[0].Targets[0].X != 0)
            throw new Exception("Target placement disagrees with runtime perspective or mutated the source before Save.");
        using var bitmap = new Bitmap(preview.Width, preview.Height);
        preview.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
        foreach (var size in new[] { new System.Drawing.Size(1100, 650), new System.Drawing.Size(760, 520) })
        {
            editor.Size = size; Application.DoEvents();
            var split = Descendants(editor).OfType<SplitContainer>().Last();
            if (split.Panel1.Width < 200 || split.Panel2.Width < 180)
                throw new Exception("Small range editor lost its perspective view or property controls.");
            if (Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                using var capture = new Bitmap(editor.Width, editor.Height);
                editor.DrawToBitmap(capture, new System.Drawing.Rectangle(System.Drawing.Point.Empty, capture.Size));
                capture.Save(Path.Combine(folder, $"archery-range-editor-{size.Width}.png"));
            }
        }
        editor.Result.Rounds[0].Targets[0].PeriodSeconds = 0;
        preview.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
        if (editor.Result.IsValid) throw new Exception("Invalid target motion was accepted.");
        System.Console.WriteLine("PASS: real archery editor placement, detached draft, perspective preview and invalid-motion rendering.");
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
}
