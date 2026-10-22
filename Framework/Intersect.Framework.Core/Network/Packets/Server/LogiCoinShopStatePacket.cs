using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class LogiCoinShopStatePacket : IntersectPacket
{
    public LogiCoinShopStatePacket()
    {
    }

    public LogiCoinShopStatePacket(
        string configurationJson,
        int balance,
        long premiumUntilUnixMilliseconds,
        bool purchaseSucceeded = false,
        string message = ""
    )
    {
        ConfigurationJson = configurationJson;
        Balance = balance;
        PremiumUntilUnixMilliseconds = premiumUntilUnixMilliseconds;
        PurchaseSucceeded = purchaseSucceeded;
        Message = message ?? string.Empty;
    }

    [Key(6)]public int CharacterLimit {get;set;}
    [Key(5)]public LogiCoinPurchaseHistoryRow[] History {get;set;}=[];

    [Key(0)]
    public string ConfigurationJson { get; set; } = string.Empty;

    [Key(1)]
    public int Balance { get; set; }

    [Key(2)]
    public long PremiumUntilUnixMilliseconds { get; set; }

    [Key(3)]
    public bool PurchaseSucceeded { get; set; }

    [Key(4)]
    public string Message { get; set; } = string.Empty;
}
