using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalStylist;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Intersect.Server.Networking;

namespace Intersect.Server.RoyalStylist;

internal static class RoyalStylistRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName =
        Path.Combine("resources", "royal-stylist.json");

    private static readonly Dictionary<Guid, Guid> OpenStylistsByPlayer = [];
    private static RoyalStylistConfiguration? _current;

    internal static RoyalStylistConfiguration Current
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

    internal static void Save(string json)
    {
        var configuration = RoyalStylistConfiguration.FromJson(json);

        foreach (var stylist in configuration.Stylists)
        {
            if (stylist.Price > 0 &&
                ItemDescriptor.Get(stylist.CurrencyItemId) == null)
            {
                throw new InvalidDataException(
                    $"Royal Stylist '{stylist.Name}' references missing currency item {stylist.CurrencyItemId}."
                );
            }
        }

        lock (Gate)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(Path.GetFullPath(PathName))!
            );
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
            RoyalStylistConfiguration.Load(configuration.ToJson());
        }
    }

    internal static RoyalStylistStatePacket Open(
        Player player,
        Guid stylistId,
        string message = ""
    )
    {
        var stylist = Current.Find(stylistId);
        if (stylist == null)
        {
            return EmptyState(
                "Royal Stylist",
                "This Royal Stylist is not configured."
            );
        }

        lock (Gate)
        {
            OpenStylistsByPlayer[player.Id] = stylist.Id;
        }

        return BuildState(player, stylist, message);
    }

    internal static RoyalStylistStatePacket Apply(
        Player player,
        Guid stylistId,
        CharacterAppearance requested
    )
    {
        RoyalStylistDefinition? stylist;
        lock (Gate)
        {
            if (!OpenStylistsByPlayer.TryGetValue(player.Id, out var openId) ||
                openId != stylistId)
            {
                return EmptyState(
                    "Royal Stylist",
                    "Open this Royal Stylist again before applying changes."
                );
            }

            stylist = Current.Find(openId);
        }

        if (stylist == null)
        {
            return EmptyState(
                "Royal Stylist",
                "This Royal Stylist is no longer configured."
            );
        }

        if (stylist.PremiumRequired && !IsPremiumActive(player))
        {
            return BuildState(
                player,
                stylist,
                "Premium membership is required to use this stylist."
            );
        }

        requested ??= new CharacterAppearance();
        var currentAppearance =
            player.Appearance ?? new CharacterAppearance();

        if (!ValidateRequestedAppearance(
                stylist,
                currentAppearance,
                requested,
                out var error
            ))
        {
            return BuildState(player, stylist, error);
        }

        var sanitized = requested.SanitizedCopy();
        if (SameAppearance(currentAppearance, sanitized))
        {
            return BuildState(
                player,
                stylist,
                "No appearance changes were selected."
            );
        }

        lock (player.EntityLock)
        {
            if (stylist.Price > 0)
            {
                if (stylist.CurrencyItemId == Guid.Empty ||
                    !player.TryTakeItem(
                        stylist.CurrencyItemId,
                        stylist.Price
                    ))
                {
                    return BuildState(
                        player,
                        stylist,
                        $"You need {stylist.Price:N0} {CurrencyName(stylist)}."
                    );
                }
            }

            player.Appearance = sanitized;
            PacketSender.SendEntityDataToProximity(player);
        }

        // Persist the appearance and the currency deduction together with the
        // rest of the account/player graph.
        player.User?.Save(force: true);

        return BuildState(
            player,
            stylist,
            "Your new appearance has been applied."
        );
    }

    private static RoyalStylistStatePacket BuildState(
        Player player,
        RoyalStylistDefinition stylist,
        string message
    )
    {
        var premiumActive = IsPremiumActive(player);
        var balance = stylist.CurrencyItemId == Guid.Empty
            ? 0
            : player.CountItems(stylist.CurrencyItemId);

        var hasAccess = !stylist.PremiumRequired || premiumActive;
        var canAfford = stylist.Price <= 0 || balance >= stylist.Price;
        var canApply = hasAccess && canAfford;

        if (string.IsNullOrWhiteSpace(message))
        {
            if (!hasAccess)
            {
                message =
                    "Premium membership is required to use this stylist.";
            }
            else if (!canAfford)
            {
                message =
                    $"You need {stylist.Price:N0} {CurrencyName(stylist)}.";
            }
        }

        return new RoyalStylistStatePacket(
            stylist.Id,
            stylist.Name,
            stylist.Description,
            (player.Appearance ?? new CharacterAppearance()).SanitizedCopy(),
            stylist.HairStyles,
            stylist.ShirtStyles,
            stylist.PantsStyles,
            stylist.BootsStyles,
            stylist.CurrencyItemId,
            CurrencyName(stylist),
            stylist.Price,
            balance,
            stylist.PremiumRequired,
            premiumActive,
            canApply,
            message
        );
    }

    private static RoyalStylistStatePacket EmptyState(
        string name,
        string message
    ) =>
        new(
            Guid.Empty,
            name,
            string.Empty,
            new CharacterAppearance(),
            [],
            [],
            [],
            [],
            Guid.Empty,
            string.Empty,
            0,
            0,
            true,
            false,
            false,
            message
        );

    private static bool IsPremiumActive(Player player)
    {
        var variableId =
            LogiCoinShopRuntime.Current.PremiumUntilUserVariableId;

        if (variableId == Guid.Empty || player.User == null)
        {
            return false;
        }

        var expiresAt =
            player.User.GetVariableValue(variableId).Integer;

        return expiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string CurrencyName(RoyalStylistDefinition stylist)
    {
        if (stylist.Price <= 0)
        {
            return "Aureons";
        }

        return ItemDescriptor.GetName(stylist.CurrencyItemId) is { Length: > 0 } name
            ? name
            : "Aureons";
    }

    private static bool ValidateRequestedAppearance(
        RoyalStylistDefinition stylist,
        CharacterAppearance current,
        CharacterAppearance requested,
        out string error
    )
    {
        if (!Allowed(
                requested.HairStyle,
                current.HairStyle,
                stylist.HairStyles,
                CharacterAppearance.HairPrefix,
                stylist.AllowNoHair
            ))
        {
            error = "That hairstyle is not offered by this stylist.";
            return false;
        }

        if (!Allowed(
                requested.ShirtStyle,
                current.ShirtStyle,
                stylist.ShirtStyles,
                CharacterAppearance.ShirtPrefix,
                stylist.AllowNoShirt
            ))
        {
            error = "That shirt is not offered by this stylist.";
            return false;
        }

        if (!Allowed(
                requested.PantsStyle,
                current.PantsStyle,
                stylist.PantsStyles,
                CharacterAppearance.PantsPrefix,
                stylist.AllowNoPants
            ))
        {
            error = "Those pants are not offered by this stylist.";
            return false;
        }

        if (!Allowed(
                requested.BootsStyle,
                current.BootsStyle,
                stylist.BootsStyles,
                CharacterAppearance.BootsPrefix,
                stylist.AllowNoBoots
            ))
        {
            error = "Those boots are not offered by this stylist.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool Allowed(
        string? proposed,
        string? current,
        IEnumerable<string> choices,
        string prefix,
        bool allowNone
    )
    {
        proposed = proposed?.Trim() ?? string.Empty;
        current = current?.Trim() ?? string.Empty;

        if (!CharacterAppearance.IsCatalogStyle(proposed, prefix))
        {
            return false;
        }

        if (string.Equals(
                proposed,
                current,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(proposed))
        {
            return allowNone;
        }

        return choices.Contains(
            proposed,
            StringComparer.OrdinalIgnoreCase
        );
    }

    private static bool SameAppearance(
        CharacterAppearance left,
        CharacterAppearance right
    ) =>
        string.Equals(
            left.HairStyle,
            right.HairStyle,
            StringComparison.OrdinalIgnoreCase
        ) &&
        left.HairColor == right.HairColor &&
        string.Equals(
            left.ShirtStyle,
            right.ShirtStyle,
            StringComparison.OrdinalIgnoreCase
        ) &&
        left.ShirtColor == right.ShirtColor &&
        string.Equals(
            left.PantsStyle,
            right.PantsStyle,
            StringComparison.OrdinalIgnoreCase
        ) &&
        left.PantsColor == right.PantsColor &&
        string.Equals(
            left.BootsStyle,
            right.BootsStyle,
            StringComparison.OrdinalIgnoreCase
        ) &&
        left.BootsColor == right.BootsColor;

    private static RoyalStylistConfiguration LoadCore()
    {
        try
        {
            var configuration = File.Exists(PathName)
                ? RoyalStylistConfiguration.FromJson(
                    File.ReadAllText(PathName)
                )
                : new RoyalStylistConfiguration();

            RoyalStylistConfiguration.Load(configuration.ToJson());
            return configuration;
        }
        catch
        {
            var empty = new RoyalStylistConfiguration();
            RoyalStylistConfiguration.Load(empty.ToJson());
            return empty;
        }
    }
}
