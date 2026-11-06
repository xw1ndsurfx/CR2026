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

    internal enum StepState { Pending, Active, Complete }

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

    internal static StepState Step(ExportationRun run, int index)
    {
        if (index is < 0 or > 2) return StepState.Pending;
        if (run.Stage == DeliveryStage.Delivered) return StepState.Complete;
        if (run.Stage is DeliveryStage.Lost or DeliveryStage.Abandoned)
            return index == 0 ? StepState.Active : StepState.Pending;
        if (run.Stage == DeliveryStage.Preparing)
            return index == 0 ? StepState.Active : StepState.Pending;
        if (index == 0) return StepState.Complete;
        if (index == 1) return run.ArrivedByShip ? StepState.Complete : StepState.Active;
        return run.ArrivedByShip ? StepState.Active : StepState.Pending;
    }

    internal static bool CanPack(ExportationRoute route, ExportationRun run, int index)
    {
        if (run.Stage != DeliveryStage.Preparing || index < 0 || index >= route.Lines.Length)
            return false;
        var line = route.Lines[index];
        return ExportationRules.Packed(run, line.ItemId) < line.Quantity;
    }
}
