namespace Intersect.Client.Interface;
internal static class AccountCharacterSlots
{
    private static int _limit;
    internal static int Limit=>_limit>0?_limit:Options.Instance.Player.MaxCharacters;
    internal static void Observe(int limit)=>_limit=Math.Clamp(limit,0,1000);
}
