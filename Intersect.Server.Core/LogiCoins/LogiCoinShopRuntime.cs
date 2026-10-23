using System.Net.Http.Json;
using System.Text.Json;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.LogiCoins;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Networking;

namespace Intersect.Server.LogiCoins;

internal static class LogiCoinShopRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "logicoins-shop.json");
    private static LogiCoinShopConfiguration? _current;

    internal static LogiCoinShopConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                return _current ??= LoadCore();
            }
        }
    }

    internal static string Json
    {
        get
        {
            lock (Gate)
            {
                return Current.ToJson();
            }
        }
    }

    internal static string PublicJson
    {
        get
        {
            lock (Gate)
            {
                var source = Current;
                var publicConfiguration = LogiCoinShopConfiguration.FromJson(source.ToJson());
                publicConfiguration.Offers = (source.Offers ?? [])
                    .Where(offer => offer.Enabled)
                    .OrderBy(offer => offer.SortOrder)
                    .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return publicConfiguration.ToJson();
            }
        }
    }

    internal static bool IsValid(LogiCoinShopConfiguration configuration)
    {
        if (!configuration.IsStructurallyValid)
        {
            return false;
        }

        if (configuration.PremiumUntilUserVariableId != Guid.Empty &&
            UserVariableDescriptor.Get(configuration.PremiumUntilUserVariableId) is not { DataType: VariableDataType.Integer })
        {
            return false;
        }

        foreach (var offer in configuration.Offers ?? [])
        {
            if (offer.Type == LogiCoinOfferType.Item && ItemDescriptor.Get(offer.ItemId) == null)
            {
                return false;
            }

            if (offer.Type == LogiCoinOfferType.Bundle &&
                (offer.Bundle ?? []).Any(entry => ItemDescriptor.Get(entry.ItemId) == null))
            {
                return false;
            }

            if (offer.Type == LogiCoinOfferType.Premium &&
                configuration.PremiumUntilUserVariableId == Guid.Empty)
            {
                return false;
            }
        }

        return true;
    }

    internal static void Save(string json)
    {
        var configuration = LogiCoinShopConfiguration.FromJson(json);
        if (!IsValid(configuration))
        {
            throw new InvalidDataException(
                "LogiCoin Shop configuration references missing items or an invalid Premium User Variable."
            );
        }

        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
        }
    }

    internal static LogiCoinShopOffer? FindOffer(Guid offerId) =>
        (Current.Offers ?? []).FirstOrDefault(offer => offer.Id == offerId);

    private static LogiCoinShopConfiguration LoadCore()
    {
        if (!File.Exists(PathName))
        {
            return new LogiCoinShopConfiguration();
        }

        try
        {
            var configuration = LogiCoinShopConfiguration.FromJson(File.ReadAllText(PathName));
            return IsValid(configuration) ? configuration : new LogiCoinShopConfiguration();
        }
        catch
        {
            return new LogiCoinShopConfiguration();
        }
    }
}

internal sealed record LogiCoinWalletResult(
    bool Ok,
    int Balance,
    int StatusCode,
    string Error,
    bool IdempotentReplay = false
);

internal static class LogiCoinWalletService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(12),
    };

    internal static LogiCoinWalletResult GetBalance(string username) =>
        Send("/game/corps-royaux/wallet", new { username });

    internal static LogiCoinWalletResult Spend(string username, int amount, string reference, string description) =>
        Send(
            "/game/corps-royaux/wallet/spend",
            new
            {
                username,
                amount,
                reference,
                description,
            }
        );

    internal static LogiCoinWalletResult Refund(string username, int amount, string reference, string originalReference) =>
        Send(
            "/game/corps-royaux/wallet/refund",
            new
            {
                username,
                amount,
                reference,
                original_reference = originalReference,
            }
        );

    private static LogiCoinWalletResult Send(string route, object payload)
    {
        var options = Options.Instance?.Logiklik;
        var baseUrl = (options?.ApiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        var gameKey = (options?.GameKey ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(gameKey))
        {
            return new LogiCoinWalletResult(
                false,
                0,
                0,
                "Logiklik integration is not configured on the Corps Royaux server."
            );
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + route);
            request.Headers.TryAddWithoutValidation("X-Logiklik-Game-Key", gameKey);
            request.Content = JsonContent.Create(payload);

            using var response = Http.Send(request);
            var raw = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var json = string.IsNullOrWhiteSpace(raw) ? null : JsonDocument.Parse(raw);
            var root = json?.RootElement;

            var balance = root.HasValue && root.Value.TryGetProperty("balance", out var balanceProperty)
                ? balanceProperty.GetInt32()
                : 0;
            var ok = response.IsSuccessStatusCode &&
                     (!root.HasValue ||
                      !root.Value.TryGetProperty("ok", out var okProperty) ||
                      okProperty.ValueKind != JsonValueKind.False);
            var error = root.HasValue && root.Value.TryGetProperty("error", out var errorProperty)
                ? errorProperty.GetString() ?? string.Empty
                : string.Empty;
            var replay = root.HasValue &&
                         root.Value.TryGetProperty("idempotent_replay", out var replayProperty) &&
                         replayProperty.ValueKind == JsonValueKind.True;

            return new LogiCoinWalletResult(ok, balance, (int)response.StatusCode, error, replay);
        }
        catch (Exception exception)
        {
            return new LogiCoinWalletResult(false, 0, 0, exception.Message);
        }
    }
}

internal sealed class LogiCoinPurchaseRecord
{
    public Guid PurchaseId { get; set; }

    public Guid UserId { get; set; }
    public Guid RecipientUserId {get;set;}
    public Guid RecipientCharacterId {get;set;}
    public string RecipientName {get;set;}="";
    public string OfferName {get;set;}="";
    public string OfferJson {get;set;}="";

    public Guid OfferId { get; set; }

    public int Price { get; set; }

    public bool WalletDebited { get; set; }

    public HashSet<int> DeliveredEntries { get; set; } = [];

    public bool PremiumApplied { get; set; }
    public int CharacterSlotsGranted {get;set;}

    public bool Completed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string LastError { get; set; } = string.Empty;
}

internal static class LogiCoinPurchaseJournal
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "logicoins-shop-purchases.json");
    private static Dictionary<Guid, LogiCoinPurchaseRecord>? _records;

    internal static LogiCoinPurchaseRecord GetOrCreate(Guid purchaseId, Guid userId, Guid offerId, int price, Guid recipientUser=default,Guid recipientCharacter=default,string recipientName="",LogiCoinShopOffer? offer=null)
    {
        lock (Gate)
        {
            var records = Records;
            if (records.TryGetValue(purchaseId, out var existing))
            {
                if (existing.UserId != userId || existing.OfferId != offerId || existing.Price != price || existing.RecipientUserId!=recipientUser || existing.RecipientCharacterId!=recipientCharacter)
                {
                    throw new InvalidOperationException("Purchase id was reused for a different LogiCoin offer.");
                }

                return Clone(existing);
            }

            var record = new LogiCoinPurchaseRecord
            {
                PurchaseId = purchaseId,
                UserId = userId,
                OfferId = offerId,
                Price = price,RecipientUserId=recipientUser,RecipientCharacterId=recipientCharacter,RecipientName=recipientName,OfferName=offer?.Name??"",OfferJson=offer==null?"":JsonSerializer.Serialize(offer),
            };
            records[purchaseId] = Clone(record);
            try{SaveCore();}catch{records.Remove(purchaseId);throw;}
            return Clone(record);
        }
    }

    internal static void Save(LogiCoinPurchaseRecord record)
    {
        lock (Gate)
        {
            var records=Records;var previous=records.GetValueOrDefault(record.PurchaseId);
            records[record.PurchaseId] = Clone(record);
            try{SaveCore();}catch{if(previous==null)records.Remove(record.PurchaseId);else records[record.PurchaseId]=previous;throw;}
        }
    }

    internal static int ExtraCharacterSlots(Guid userId)
    {
        lock(Gate)return (int)Math.Min(100,Records.Values.Where(record=>(record.RecipientUserId==Guid.Empty?record.UserId:record.RecipientUserId)==userId && record.WalletDebited)
            .Sum(record=>(long)Math.Clamp(record.CharacterSlotsGranted,0,20)));
    }
    internal static LogiCoinPurchaseHistoryRow[] History(Guid userId)
    {
        lock(Gate)return Records.Values.Where(record=>record.UserId==userId && record.WalletDebited).OrderByDescending(record=>record.CreatedAtUtc).Take(100)
            .Select(record=>new LogiCoinPurchaseHistoryRow{Offer=record.OfferName,Recipient=record.RecipientName,Price=record.Price,Completed=record.Completed,CreatedUtc=record.CreatedAtUtc.ToUnixTimeMilliseconds()}).ToArray();
    }
    internal static IEnumerable<LogiCoinPurchaseRecord> Pending(Guid userId)
    {
        lock (Gate)
        {
            return Records.Values
                .Where(record => (record.RecipientUserId==Guid.Empty?record.UserId:record.RecipientUserId)==userId && !record.Completed && record.WalletDebited)
                .Select(Clone)
                .ToArray();
        }
    }

    private static Dictionary<Guid, LogiCoinPurchaseRecord> Records
    {
        get
        {
            if (_records != null)
            {
                return _records;
            }

            try
            {
                _records = File.Exists(PathName)
                    ? JsonSerializer.Deserialize<Dictionary<Guid, LogiCoinPurchaseRecord>>(File.ReadAllText(PathName)) ?? []
                    : [];
            }
            catch(Exception error)
            {
                throw new InvalidDataException("The purchase journal cannot be read. Existing purchases were preserved.",error);
            }

            return _records;
        }
    }

    private static LogiCoinPurchaseRecord Clone(LogiCoinPurchaseRecord source) =>
        new()
        {
            PurchaseId = source.PurchaseId,
            UserId = source.UserId,RecipientUserId=source.RecipientUserId,RecipientCharacterId=source.RecipientCharacterId,RecipientName=source.RecipientName,OfferName=source.OfferName,OfferJson=source.OfferJson,
            OfferId = source.OfferId,
            Price = source.Price,
            WalletDebited = source.WalletDebited,
            DeliveredEntries = new HashSet<int>(source.DeliveredEntries ?? []),
            PremiumApplied = source.PremiumApplied,
            CharacterSlotsGranted=source.CharacterSlotsGranted,
            Completed = source.Completed,
            CreatedAtUtc = source.CreatedAtUtc,
            LastError = source.LastError,
        };

    private static void SaveCore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathName))!);
        var temp = PathName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Records));
        File.Move(temp, PathName, overwrite: true);
    }
}

internal static partial class LogiCoinPurchaseRuntime
{
    internal static LogiCoinShopStatePacket BuildState(Player player, bool purchaseSucceeded = false, string message = "")
    {
        TryResumePending(player);

        var wallet = LogiCoinWalletService.GetBalance(player.User.Name);
        var premiumUntil = PremiumUntil(player);

        return new LogiCoinShopStatePacket(
            LogiCoinShopRuntime.PublicJson,
            wallet.Ok ? wallet.Balance : 0,
            premiumUntil,
            purchaseSucceeded,
            wallet.Ok ? message : (string.IsNullOrWhiteSpace(wallet.Error) ? "Unable to load LogiCoins." : wallet.Error)
        ){History=LogiCoinPurchaseJournal.History(player.User.Id),CharacterLimit=CharacterLimit(player.User.Id)};
    }

    internal static int CharacterLimit(Guid userId)
    {
        try{return Math.Clamp(Options.Instance.Player.MaxCharacters+LogiCoinPurchaseJournal.ExtraCharacterSlots(userId),1,1000);}
        catch(InvalidDataException){return Math.Max(1,Options.Instance.Player.MaxCharacters);}
    }

    private static readonly object PurchaseGate=new();
    internal static LogiCoinShopStatePacket Purchase(Player player, Guid offerId, Guid purchaseId,Guid recipientUserId=default,Guid recipientCharacterId=default)
    {
        lock(player.EntityLock)
        lock(PurchaseGate)return PurchaseCore(player,offerId,purchaseId,recipientUserId,recipientCharacterId);
    }
    private static LogiCoinShopStatePacket PurchaseCore(Player player, Guid offerId, Guid purchaseId,Guid recipientUserId,Guid recipientCharacterId)
    {
        var configuration = LogiCoinShopRuntime.Current;
        if (!configuration.Enabled)
        {
            return BuildState(player, false, "The LogiCoin Shop is currently unavailable.");
        }

        var offer = LogiCoinShopRuntime.FindOffer(offerId);
        if (offer is not { Enabled: true })
        {
            return BuildState(player, false, "This offer is no longer available.");
        }

        if (purchaseId == Guid.Empty)
        {
            return BuildState(player, false, "Invalid purchase request.");
        }

        var gifting=recipientUserId!=Guid.Empty;
        var recipientName=player.Name;
        if(gifting)
        {
            if(!offer.AllowGifts)return BuildState(player,false,"This offer cannot be gifted.");
            var recipient=Intersect.Server.Database.PlayerData.User.FindById(recipientUserId);
            if(recipient==null)return BuildState(player,false,"Recipient account not found.");
            recipientName=recipient.Name;
            if(!offer.AccountDelivery)
            {
                using var context=Intersect.Server.Database.DbInterface.CreatePlayerContext(readOnly:true);
                var character=context.Players.Where(character=>character.UserId==recipientUserId && character.Id==recipientCharacterId).Select(character=>new{character.Name}).FirstOrDefault();
                if(character==null)return BuildState(player,false,"Choose a character belonging to the recipient account.");
                recipientName+=" / "+character.Name;
            }
            else recipientCharacterId=Guid.Empty;
        }
        else recipientCharacterId=offer.AccountDelivery?Guid.Empty:player.Id;
        var now = DateTimeOffset.UtcNow;
        var price = offer.EffectivePrice(now);
        var record = LogiCoinPurchaseJournal.GetOrCreate(purchaseId, player.User.Id, offer.Id, price,recipientUserId,recipientCharacterId,recipientName,offer);
        var spendReference = $"ingame:{player.User.Id:N}:{purchaseId:N}:{offer.Id:N}";

        if (!record.WalletDebited)
        {
            if(offer.Type==LogiCoinOfferType.CharacterSlot && LogiCoinPurchaseJournal.ExtraCharacterSlots(gifting?recipientUserId:player.User.Id)+offer.CharacterSlots>configuration.MaximumExtraCharacterSlots)
                return BuildState(player,false,"The account character slot limit has been reached.");
            if (price > 0)
            {
                var spend = LogiCoinWalletService.Spend(player.User.Name, price, spendReference, offer.Name);
                if (!spend.Ok)
                {
                    record.LastError = spend.Error;
                    LogiCoinPurchaseJournal.Save(record);
                    return BuildState(
                        player,
                        false,
                        spend.StatusCode == 409
                            ? $"Not enough LogiCoins. Balance: {spend.Balance:N0}."
                            : (string.IsNullOrWhiteSpace(spend.Error) ? "LogiCoin payment failed." : spend.Error)
                    );
                }
            }

            record.WalletDebited = true;
            if(offer.Type==LogiCoinOfferType.CharacterSlot)
            {record.CharacterSlotsGranted=offer.CharacterSlots;record.Completed=true;}
            record.LastError = string.Empty;
            LogiCoinPurchaseJournal.Save(record);
        }

        if(gifting)return BuildState(player,true,record.Completed?"Gift delivered successfully.":"Gift purchased. Delivery is queued for the recipient.");
        if (!TryDeliver(player, offer, record, out var deliveryError))
        {
            record.LastError = deliveryError;
            LogiCoinPurchaseJournal.Save(record);
            return BuildState(
                player,
                false,
                "Payment received. Delivery will retry automatically."
            );
        }

        record.Completed = true;
        record.LastError = string.Empty;
        LogiCoinPurchaseJournal.Save(record);

        return BuildState(player, true, "Purchase completed.");
    }

    internal static void TryResumePending(Player player)
    {
        lock(player.EntityLock)
        {
        foreach (var record in LogiCoinPurchaseJournal.Pending(player.User.Id))
        {
            if(record.RecipientCharacterId!=Guid.Empty && record.RecipientCharacterId!=player.Id)continue;
            var offer = record.OfferJson.Length>0?JsonSerializer.Deserialize<LogiCoinShopOffer>(record.OfferJson):LogiCoinShopRuntime.FindOffer(record.OfferId);
            if (offer == null)
            {
                continue;
            }

            if (TryDeliver(player, offer, record, out var error))
            {
                record.Completed = true;
                record.LastError = string.Empty;
            }
            else
            {
                record.LastError = error;
            }

            LogiCoinPurchaseJournal.Save(record);
        }
        }
    }

    private static bool TryDeliver(
        Player player,
        LogiCoinShopOffer offer,
        LogiCoinPurchaseRecord record,
        out string error
    )
    {
        error = string.Empty;

        try
        {
            switch (offer.Type)
            {
                case LogiCoinOfferType.Item:
                    return DeliverItems(
                        player,
                        [new LogiCoinBundleItem(offer.ItemId, offer.ItemQuantity)],
                        record,
                        out error
                    );

                case LogiCoinOfferType.Bundle:
                    return DeliverItems(player, offer.Bundle ?? [], record, out error);

                case LogiCoinOfferType.CharacterSlot:
                    if(record.CharacterSlotsGranted==0)
                    {
                        record.CharacterSlotsGranted=offer.CharacterSlots;
                        LogiCoinPurchaseJournal.Save(record);
                    }
                    return true;
                case LogiCoinOfferType.Premium:
                    return DeliverPremium(player, offer, record, out error);

                default:
                    error = "Unsupported offer type.";
                    return false;
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool DeliverItems(
        Player player,
        IReadOnlyList<LogiCoinBundleItem> items,
        LogiCoinPurchaseRecord record,
        out string error
    )
    {
        error = string.Empty;

        for (var index = 0; index < items.Count; ++index)
        {
            if (record.DeliveredEntries.Contains(index))
            {
                continue;
            }

            var entry = items[index];
            if (ItemDescriptor.Get(entry.ItemId) is not { } item)
            {
                error = $"Item {entry.ItemId} no longer exists.";
                return false;
            }

            if(!HasDeliveryReceipt(player,record,index) && !CommitItemDelivery(player,record,index,entry,out error))return false;

            record.DeliveredEntries.Add(index);
            LogiCoinPurchaseJournal.Save(record);
        }

        return true;
    }

    private static bool DeliverPremium(
        Player player,
        LogiCoinShopOffer offer,
        LogiCoinPurchaseRecord record,
        out string error
    )
    {
        error = string.Empty;

        if (record.PremiumApplied || HasDeliveryReceipt(player,record,-1))
        {
            record.PremiumApplied=true;
            return true;
        }

        var variableId = LogiCoinShopRuntime.Current.PremiumUntilUserVariableId;
        var descriptor = UserVariableDescriptor.Get(variableId);
        if (descriptor is not { DataType: VariableDataType.Integer })
        {
            error = "Premium expiry User Variable is not configured.";
            return false;
        }

        var value = player.User.GetVariableValue(variableId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = Math.Max(now, value.Integer);
        var extension = checked((long)offer.PremiumDays * 24L * 60L * 60L * 1000L);
        var previous=value.Integer;
        value.Integer = checked(start + extension);
        var receipt=AddDeliveryReceipt(player,record,-1);
        if(player.User.Save(force:true)!=Intersect.Server.Database.PlayerData.UserSaveResult.Completed)
        {
            value.Integer=previous;player.User.Variables.Remove(receipt);
            error="Premium delivery could not be saved.";return false;
        }

        player.User.UpdatedVariables.AddOrUpdate(variableId, descriptor, (_, _) => descriptor);
        player.User.StartCommonEventsWithTriggerForAll(CommonEventTrigger.UserVariableChange, string.Empty, variableId.ToString());

        record.PremiumApplied = true;
        LogiCoinPurchaseJournal.Save(record);
        return true;
    }

    internal static bool HasActivePremium(Player player)
    {
        return PremiumUntil(player) > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    internal static long PremiumUntil(Player player)
    {
        var variableId = LogiCoinShopRuntime.Current.PremiumUntilUserVariableId;
        return variableId == Guid.Empty ? 0 : player.User.GetVariableValue(variableId).Integer;
    }
}
