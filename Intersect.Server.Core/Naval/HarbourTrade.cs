namespace Intersect.Server.Naval;

internal enum HarbourTradeResult { Completed, InsufficientFunds, SaveFailed }

/// <summary>Apply both sides before the native account transaction; restore both on failure.</summary>
internal static class HarbourTrade
{
    internal static HarbourTradeResult Commit(Func<bool> pay, Action deliver, Func<bool> save, Action rollback)
    {
        var committed = false;
        try
        {
            if (!pay()) return HarbourTradeResult.InsufficientFunds;
            deliver();
            committed = save();
            return committed ? HarbourTradeResult.Completed : HarbourTradeResult.SaveFailed;
        }
        finally { if (!committed) rollback(); }
    }
}
