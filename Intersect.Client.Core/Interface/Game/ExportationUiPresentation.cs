using Intersect.Framework.Core.Exportation;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Presentation-only data derived from a server-authored Exportation run.
/// Never predicts, changes or persists packed quantities or voyage state.
/// </summary>
internal static class ExportationUiPresentation
{
    internal readonly record struct Progress(int Packed, int Required)
    {
        public bool Complete => Required > 0 && Packed == Required;
        public int Percent => Required <= 0 ? 0 :
            (int)Math.Clamp((long)Packed * 100L / Required, 0L, 100L);
    }

    internal static Progress Cargo(ExportationRoute route, ExportationRun run, bool? materials = null)
    {
        var lines = route.Lines.Where(line => materials is null ||
            (line.Group == CargoGroup.Materials) == materials.Value);
        var packed = 0;
        var required = 0;
        foreach (var line in lines)
        {
            required += line.Quantity;
            packed += Math.Clamp(ExportationRules.Packed(run, line.ItemId), 0, line.Quantity);
        }
        return new Progress(packed, required);
    }

    internal static string PackingSound(CargoLine line) => line.Sprite == "lingots_fer"
        ? "item_metal" : line.Group == CargoGroup.Fragile ? "item_glass" : "item_wood";

    internal static bool CanPack(ExportationRoute route, ExportationRun run, int index)
    {
        if (run.Stage != DeliveryStage.Preparing || index < 0 || index >= route.Lines.Length)
            return false;
        var line = route.Lines[index];
        return ExportationRules.Packed(run, line.ItemId) < line.Quantity;
    }
}
