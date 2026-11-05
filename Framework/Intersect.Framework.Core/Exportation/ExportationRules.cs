namespace Intersect.Framework.Core.Exportation;

/// <summary>
/// Server-side rules. Never trust client-supplied actor, packed quantities or
/// voyage flags. Quest and reward changes must be committed atomically.
/// </summary>
public static class ExportationRules
{
    private static bool Match(ExportationRoute r, ExportationRun s) =>
        r.IsValid && s.RouteId == r.Id && s.Packed != null;
    private static bool AtSource(ExportationRoute r, ExportationActor a) =>
        a.Alive && a.MapId == r.SourceMapId && a.NpcId == r.SourceNpcId;
    private static bool AtRecipient(ExportationRoute r, ExportationActor a) =>
        a.Alive && a.MapId == r.RecipientMapId && a.NpcId == r.RecipientNpcId;

    public static bool TryStart(ExportationRoute r, ExportationActor a, int level, out ExportationRun? run)
    {
        run = null;
        if (!r.IsValid || !AtSource(r, a) || !a.HasUsableBoat ||
            level < r.MinimumProfessionLevel) return false;
        run = new ExportationRun { RouteId = r.Id };
        return true;
    }

    public static int Packed(ExportationRun s, Guid item) => s.Packed?.GetValueOrDefault(item) ?? 0;

    public static bool TryPack(ExportationRoute r, ExportationRun s, ExportationActor a,
        Guid item, CargoGroup group, int delta)
    {
        if (!Match(r, s) || s.Stage != DeliveryStage.Preparing || !AtSource(r, a) || delta == 0)
            return false;
        var line = r.Lines.FirstOrDefault(l => l.ItemId == item && l.Group == group);
        if (line == null) return false;
        var next = (long)Packed(s, item) + delta;
        if (next < 0 || next > line.Quantity) return false;
        if (next == 0) s.Packed.Remove(item); else s.Packed[item] = (int)next;
        return true;
    }

    public static bool ReadyToSeal(ExportationRoute r, ExportationRun s) =>
        Match(r, s) && s.Packed.Count == r.Lines.Length &&
        r.Lines.All(l => Packed(s, l.ItemId) == l.Quantity);

    public static bool TrySeal(ExportationRoute r, ExportationRun s, ExportationActor a)
    {
        if (s.Stage != DeliveryStage.Preparing || !AtSource(r, a) ||
            !a.HasUsableBoat || !ReadyToSeal(r, s)) return false;
        s.CargoSealId = Guid.NewGuid();
        // A player may own several vessels docked on different islands.
        // Bind the cargo to the *actual* vessel when the server confirms
        // boarding, never to the first arbitrary vessel in the fleet.
        s.ShipId = Guid.Empty;
        s.BoardedByShip = s.ArrivedByShip = false;
        s.Stage = DeliveryStage.InTransit;
        return true;
    }

    /// <summary>Invoke from verified naval embarkation; never a client request.</summary>
    public static bool TryEmbark(ExportationRoute r, ExportationRun s, ExportationActor a)
    {
        if (!Match(r, s) || s.Stage != DeliveryStage.InTransit ||
            s.CargoSealId == Guid.Empty || a.MapId != r.SourceMapId ||
            !a.Alive || !a.IsAboard || a.ShipId == Guid.Empty ||
            (s.ShipId != Guid.Empty && a.ShipId != s.ShipId)) return false;
        s.ShipId = a.ShipId;
        s.BoardedByShip = true;
        return true;
    }

    /// <summary>Invoke from verified naval docking, retaining arrival ship ID.</summary>
    public static bool TryDock(ExportationRoute r, ExportationRun s, ExportationActor a)
    {
        if (!Match(r, s) || s.Stage != DeliveryStage.InTransit ||
            s.CargoSealId == Guid.Empty || !s.BoardedByShip ||
            !a.Alive || a.IsAboard || a.MapId != r.RecipientMapId ||
            a.ShipId == Guid.Empty || a.ShipId != s.ShipId) return false;
        s.ArrivedByShip = true;
        return true;
    }

    public static bool CanDeliver(ExportationRoute r, ExportationRun s, ExportationActor a) =>
        Match(r, s) && s.Stage == DeliveryStage.InTransit &&
        s.CargoSealId != Guid.Empty && s.ShipId != Guid.Empty &&
        s.BoardedByShip && s.ArrivedByShip && AtRecipient(r, a);

    /// <summary>
    /// Consumes the cargo once. Caller must award and persist with the quest
    /// in one transaction and restore the previous run if saving fails.
    /// </summary>
    public static bool TryDeliver(ExportationRoute r, ExportationRun s,
        ExportationActor a, out ExportationReward reward)
    {
        reward = default;
        if (!CanDeliver(r, s, a)) return false;
        reward = new ExportationReward(r.RewardItemId, r.RewardQuantity, r.ExperienceReward);
        s.CargoSealId = Guid.Empty;
        s.Stage = DeliveryStage.Delivered;
        return true;
    }

    public static bool TryLose(ExportationRun s, CargoFailure cause, Guid sunkShipId = default)
    {
        if (s.Stage != DeliveryStage.InTransit || s.CargoSealId == Guid.Empty || !Enum.IsDefined(cause))
            return false;
        if (cause == CargoFailure.ShipSunk &&
            (s.ArrivedByShip || sunkShipId == Guid.Empty || sunkShipId != s.ShipId)) return false;
        s.CargoSealId = Guid.Empty;
        s.ShipId = Guid.Empty;
        s.BoardedByShip = s.ArrivedByShip = false;
        s.Packed.Clear();
        s.LastFailure = cause;
        s.Stage = DeliveryStage.Lost;
        return true;
    }

    /// <summary>Retry the same contract at its original merchant.</summary>
    public static bool TryRestart(ExportationRoute r, ExportationRun s, ExportationActor a)
    {
        if (!Match(r, s) || s.Stage != DeliveryStage.Lost ||
            !AtSource(r, a) || !a.HasUsableBoat) return false;
        s.Packed.Clear();
        s.CargoSealId = s.ShipId = Guid.Empty;
        s.BoardedByShip = s.ArrivedByShip = false;
        s.Attempt = s.Attempt < int.MaxValue ? Math.Max(1, s.Attempt) + 1 : int.MaxValue;
        s.Stage = DeliveryStage.Preparing;
        return true;
    }

    public static bool TryAbandon(ExportationRun s)
    {
        if (s.Stage is DeliveryStage.Delivered or DeliveryStage.Abandoned) return false;
        s.CargoSealId = s.ShipId = Guid.Empty;
        s.Packed.Clear();
        s.Stage = DeliveryStage.Abandoned;
        return true;
    }
}
