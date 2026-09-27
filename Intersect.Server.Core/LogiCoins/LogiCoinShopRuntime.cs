using System.Text.Json;
using Intersect.Enums;
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

    internal static LogiCoinWalletResult GetBalance(string email) =>
        Send("/game/corps-royaux/wallet", new { email });

    internal static LogiCoinWalletResult Spend(string email, int amount, string reference, string description) =>
        Send(
            "/game/corps-royaux/wallet/spend",
            new
            {
                email,
                amount,
                reference,
                description,
            }
        );

    internal static LogiCoinWalletResult Refund(string email, int amount, string reference, string originalReference) =>
        Send(
            "/game/corps-royaux/wallet/refund",
            new
            {
                email,
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

    public Guid OfferId { get; set; }

    public int Price { get; set; }

    public bool WalletDebited { get; set; }

    public HashSet<int> DeliveredEntries { get; set; } = [];

    public bool PremiumApplied { get; set; }

    public bool Completed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string LastError { get; set; } = string.Empty;
}

internal static class LogiCoinPurchaseJournal
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "logicoins-shop-purchases.json");
    private static Dictionary<Guid, LogiCoinPurchaseRecord>? _records;

    internal static LogiCoinPurchaseRecord GetOrCreate(Guid purchaseId, Guid userId, Guid offerId, int price)
    {
        lock (Gate)
        {
            var records = Records;
            if (records.TryGetValue(purchaseId, out var existing))
            {
                if (existing.UserId != userId || existing.OfferId != offerId || existing.Price != price)
                {
                    throw new InvalidOperationException("Purchase id was reused for a different LogiCoin offer.");
                }

                return existing;
            }

            var record = new LogiCoinPurchaseRecord
            {
                PurchaseId = purchaseId,
                UserId = userId,
                OfferId = offerId,
                Price = price,
            };
            records[purchaseId] = record;
            SaveCore();
            return record;
        }
    }

    internal static void Save(LogiCoinPurchaseRecord record)
    {
        lock (Gate)
        {
            Records[record.PurchaseId] = record;
            SaveCore();
        }
    }

    internal static IEnumerable<LogiCoinPurchaseRecord> Pending(Guid userId)
    {
        lock (Gate)
        {
            return Records.Values
                .Where(record => record.UserId == userId && !record.Completed && record.WalletDebited)
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
            catch
            {
                _records = [];
            }

            return _records;
        }
    }

    private static LogiCoinPurchaseRecord Clone(LogiCoinPurchaseRecord source) =>
        new()
        {
            PurchaseId = source.PurchaseId,
            UserId = source.UserId,
            OfferId = source.OfferId,
            Price = source.Price,
            WalletDebited = source.WalletDebited,
            DeliveredEntries = new HashSet<int>(source.DeliveredEntries ?? []),
            PremiumApplied = source.PremiumApplied,
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

internal static class LogiCoinPurchaseRuntime
{
    internal static LogiCoinShopStatePacket BuildState(Player player, bool purchaseSucceeded = false, string message = "")
    {
        TryResumePending(player);

        var wallet = LogiCoinWalletService.GetBalance(player.User.Email);
        var premiumUntil = PremiumUntil(player);

        return new LogiCoinShopStatePacket(
            LogiCoinShopRuntime.PublicJson,
            wallet.Ok ? wallet.Balance : 0,
            premiumUntil,
            purchaseSucceeded,
            wallet.Ok ? message : (string.IsNullOrWhiteSpace(wallet.Error) ? "Unable to load LogiCoins." : wallet.Error)
        );
    }

    internal static LogiCoinShopStatePacket Purchase(Player player, Guid offerId, Guid purchaseId)
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

        var now = DateTimeOffset.UtcNow;
        var price = offer.EffectivePrice(now);
        var record = LogiCoinPurchaseJournal.GetOrCreate(purchaseId, player.User.Id, offer.Id, price);
        var spendReference = $"ingame:{player.User.Id:N}:{purchaseId:N}:{offer.Id:N}";

        if (!record.WalletDebited)
        {
            var spend = LogiCoinWalletService.Spend(player.User.Email, price, spendReference, offer.Name);
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

            record.WalletDebited = true;
            record.LastError = string.Empty;
            LogiCoinPurchaseJournal.Save(record);
        }

        if (!TryDeliver(player, offer, record, out var deliveryError))
        {
            record.LastError = deliveryError;
            LogiCoinPurchaseJournal.Save(record);
            return BuildState(
                player,
                false,
                $"Payment received. Delivery is pending and will retry automatically. {deliveryError}"
            );
        }

        record.Completed = true;
        record.LastError = string.Empty;
        LogiCoinPurchaseJournal.Save(record);

        return BuildState(player, true, $"{offer.Name} purchased successfully.");
    }

    private static void TryResumePending(Player player)
    {
        foreach (var record in LogiCoinPurchaseJournal.Pending(player.User.Id))
        {
            var offer = LogiCoinShopRuntime.FindOffer(record.OfferId);
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

            if (!player.TryGiveItem(entry.ItemId, entry.Quantity, ItemHandling.Normal, bankOverflow: true))
            {
                error = $"No room for {entry.Quantity:N0} x {item.Name}.";
                return false;
            }

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

        if (record.PremiumApplied)
        {
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
        value.Integer = checked(start + extension);

        player.User.UpdatedVariables.AddOrUpdate(variableId, descriptor, (_, _) => descriptor);
        player.User.StartCommonEventsWithTriggerForAll(CommonEventTrigger.UserVariableChange, string.Empty, variableId.ToString());

        record.PremiumApplied = true;
        LogiCoinPurchaseJournal.Save(record);
        return true;
    }

    private static long PremiumUntil(Player player)
    {
        var variableId = LogiCoinShopRuntime.Current.PremiumUntilUserVariableId;
        return variableId == Guid.Empty ? 0 : player.User.GetVariableValue(variableId).Integer;
    }
}
