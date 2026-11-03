using System.Reflection;
using Intersect.Editor.Forms;

internal static class CharacterPreviewZoomTests
{
    internal static void Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(FrmCharacterGenerator).GetNestedType("PixelPreview", BindingFlags.NonPublic)!;
        using var preview = (Control)Activator.CreateInstance(type)!;
        preview.Size = new Size(200, 200);
        var image = new Bitmap(20, 40); image.SetPixel(10, 20, Color.Red);
        type.GetMethod("SetImage")!.Invoke(preview, [image]);
        double Zoom() => (double)type.GetProperty("Zoom", flags)!.GetValue(preview)!;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        using var before = new Bitmap(200, 200); preview.DrawToBitmap(before, preview.ClientRectangle);
        type.GetMethod("OnMouseWheel", flags)!.Invoke(preview, [new MouseEventArgs(MouseButtons.None, 0, 100, 100, 120)]);
        Check(Zoom() == 1.25, "Mouse wheel zooms the preview");
        type.GetMethod("ZoomBy", flags)!.Invoke(preview, [100d, null]); Check(Zoom() == 8, "Maximum zoom is bounded");
        type.GetMethod("ZoomBy", flags)!.Invoke(preview, [.0001d, null]); Check(Zoom() == .25, "Minimum zoom is bounded");
        type.GetMethod("FitPreview", flags)!.Invoke(preview, null);
        using var after = new Bitmap(200, 200); preview.DrawToBitmap(after, preview.ClientRectangle);
        Check(Zoom() == 1 && image.Size == new Size(20, 40) && image.GetPixel(10,20).ToArgb() == Color.Red.ToArgb(), "Zoom preserves source pixels and dimensions");
        for (var x=0;x<200;x++) for(var y=0;y<200;y++) Check(before.GetPixel(x,y)==after.GetPixel(x,y), "Fit restores the original framing");
        CharacterWeaponPaperdollTests.Run();
        VerifyDeathEditors();
        System.Console.WriteLine("PASS character preview wheel zoom, bounds, fit, pixel preservation and exports");
    }

    private static void VerifyDeathEditors()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var animation = new Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor(Guid.NewGuid()) { Name = "Death ground" };
        Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor.Lookup.Set(animation.Id, animation);
        try
        {
            var rule = new Intersect.Framework.Core.Systems.Deaths.DeathRule { Name = "Test", MapIds = [Guid.NewGuid()], RespawnMapId = Guid.NewGuid(), GroundAnimationId = animation.Id, GroundAnimationSeconds = 45 };
            using var editor = new Intersect.Editor.Forms.Editors.FrmDeathConfiguration(new Intersect.Framework.Core.Systems.Deaths.DeathConfiguration { Rules = [rule] }.ToJson());
            var number = (NumericUpDown)editor.GetType().GetField("_groundSeconds", flags)!.GetValue(editor)!;
            if (number.Value != 45 || number.Minimum != 1 || number.Maximum != 600) throw new Exception("Death Editor must load and bound ground duration");
            number.Value = 75; editor.GetType().GetMethod("Store", flags)!.Invoke(editor, null);
            var selected = (Intersect.Framework.Core.Systems.Deaths.DeathRule)editor.GetType().GetField("_selected", flags)!.GetValue(editor)!;
            if (selected.GroundAnimationSeconds != 75 || selected.GroundAnimationId != animation.Id) throw new Exception("Death Editor must save the second animation and its duration");
            using var npcEditor = new Intersect.Editor.Forms.Editors.FrmNpc();
            var npc = new Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor(Guid.NewGuid());
            npcEditor.GetType().GetField("mEditorItem", flags)!.SetValue(npcEditor, npc);
            var npcDuration = (NumericUpDown)npcEditor.GetType().GetField("_npcDeathSeconds", flags)!.GetValue(npcEditor)!;
            npcDuration.Value = 20;
            if (npc.DeathAnimationDurationSeconds != 20) throw new Exception("NPC Editor must save the death animation duration for ordinary NPCs");
            System.Console.WriteLine("PASS NPC and player death editors persist independent animation durations");
        }
        finally { Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor.Lookup.Delete(animation); }
    }
}
