using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Archery;

internal static class NpcAutoCreatorWorkspaceTests
{
    internal static void Run()
    {
        using var owner = new Form();
        using var source = new TabControl { Dock = DockStyle.Fill };
        owner.Controls.Add(source);
        var general = new TabPage("General"); var sounds = new TabPage("Sounds"); var auto = new TabPage("Auto");
        source.TabPages.AddRange([general, sounds, auto]); source.SelectedTab = sounds;
        var field = new TextBox(); var changed = 0;
        field.TextChanged += (_, _) => changed++;
        sounds.Controls.Add(field);
        using var dialog = new Form { AutoScroll = true, AutoScrollMinSize = new Size(500, 1200) };
        var cancel = new Button { DialogResult = DialogResult.Cancel };
        var generator = new Button(); dialog.Controls.AddRange([generator, cancel]);
        var type = typeof(FrmNpc).Assembly.GetType("Intersect.Editor.Forms.Editors.NpcAutoCreatorWorkspace", true)!;
        using (var workspace = (IDisposable)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            [dialog, source, new[] { general, sounds }, cancel, "Generate", "Keep"], null)!)
        {
            if (source.TabPages.Count != 1 || general.Parent == source || sounds.Parent == source)
                throw new Exception("Auto Creator borrows every real NPC settings page.");
            field.Text = "growl.wav";
            if (changed != 1) throw new Exception("Real resource controls retain their handlers.");
            if (dialog.AutoScroll || dialog.AcceptButton == generator)
                throw new Exception("Confirmation remains outside the scrolling generator.");
        }
        if (source.TabPages.Count != 3 || source.TabPages[0] != general || source.TabPages[1] != sounds ||
            source.SelectedTab != sounds || field.IsDisposed)
            throw new Exception("NPC pages, order, selection and live controls must return to their owner.");
        System.Console.WriteLine("PASS NPC Auto Creator real settings pages, handlers, footer and ownership restoration");
        VerifySkillEditor();
        VerifyDraftTransaction();
    }

    private static void VerifySkillEditor()
    {
        var category = new MasteryCategory { Talents = [new MasteryTalent { Skill = new WeaponSkillDefinition { Enabled = true } }] };
        using var editor = new FrmMasteryTalents(category);
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var picker = (ComboBox)typeof(FrmMasteryTalents).GetField("_skillIcon", hidden)!.GetValue(editor)!;
        picker.Items.Add("test-skill.png"); picker.SelectedItem = "test-skill.png";
        if (editor.Result.Talents[0].Skill.Icon != "test-skill.png" || category.Talents[0].Skill.Icon != "")
            throw new Exception("Selecting a spell PNG changes only the detached talent draft.");
        var settings = new WeaponSkillBarSettings();
        var type = typeof(FrmMasteryTalents).Assembly.GetType("Intersect.Editor.Forms.Editors.FrmWeaponSkillBarSettings", true)!;
        using var dialog = (Form)Activator.CreateInstance(type, hidden, null, [settings], null)!;
        var size = dialog.Controls.Find("WeaponSkillSlotSize", true).OfType<NumericUpDown>().Single();
        size.Value = 48;
        var result = (WeaponSkillBarSettings)type.GetProperty("Result", hidden)!.GetValue(dialog)!;
        if (result.SlotSize != 48 || settings.SlotSize != 34 || !result.IsValid)
            throw new Exception("Quickbar parameters edit a validated detached draft.");
        System.Console.WriteLine("PASS weapon skill editor: spell PNG choice and detached quickbar parameters");
    }

    private static void VerifyDraftTransaction()
    {
        var npc = new Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor { DeathSound = "saved.wav", Level = 3 };
        npc.MakeBackup();
        npc.DeathSound = "unsaved.wav";
        npc.AdvancedBossComboEnabled = true;
        npc.Spells.Add(Guid.NewGuid()); npc.SetSpellCastIntervalMs(0, 8000);
        var type = typeof(FrmNpc).Assembly.GetType("Intersect.Editor.Forms.Editors.NpcAutoCreatorDraft", true)!;
        IDisposable Draft() => (IDisposable)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [npc], null)!;
        using (Draft())
        {
            npc.DeathSound = "changed.wav"; npc.Level = 99; npc.AdvancedBossComboEnabled = false;
            npc.Spells.Clear(); npc.SpellCastIntervalsMs.Clear();
        }
        if (npc.DeathSound != "unsaved.wav" || npc.Level != 3 || !npc.AdvancedBossComboEnabled ||
            npc.Spells.Count != 1 || npc.GetSpellCastIntervalMs(0) != 8000)
            throw new Exception("Cancel restores the pre-existing unsaved draft, including nested spell settings.");
        using (var draft = Draft())
        {
            npc.DeathSound = "accepted.wav";
            type.GetMethod("Keep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(draft, null);
        }
        if (npc.DeathSound != "accepted.wav") throw new Exception("Confirmation must keep NPC changes.");
        npc.RestoreBackup();
        if (npc.DeathSound != "saved.wav") throw new Exception("The editor's original undo backup must remain intact.");
        System.Console.WriteLine("PASS NPC draft: cancel restores unsaved values and spell timings, confirm keeps changes, original undo remains intact");
    }
}
