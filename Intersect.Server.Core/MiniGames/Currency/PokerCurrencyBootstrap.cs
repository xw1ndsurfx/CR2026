using System.Runtime.CompilerServices;

namespace Intersect.Server.MiniGames.Currency;

internal static class PokerCurrencyBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Start the existing guarded sweep even after a restart with no open poker windows.
        // No database is opened until an authenticated, online player can receive a refund.
        _ = PokerRuntime.NextSequence();
    }
}
