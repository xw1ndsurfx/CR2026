using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.QuestShops;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;

namespace Intersect.Server.QuestShops;

internal static class QuestShopRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "quest-shops.json");
    private static readonly Dictionary<Guid, Guid> OpenShopsByPlayer = [];
    private static QuestShopConfiguration? _current;

    internal static QuestShopConfiguration Current
    {
        get
        {
            lock (Gate)
                return _current ??= LoadCore();
        }
    }

    internal static string Json
    {
        get
        {
            lock (Gate)
                return Current.ToJson();
        }
    }

    internal static void Save(string json)
    {
        var configuration = QuestShopConfiguration.FromJson(json);
        foreach (var questId in configuration.Shops.SelectMany(shop => shop.QuestIds))
        {
            if (QuestDescriptor.Get(questId) == null)
                throw new InvalidDataException($"Quest Shop references missing quest {questId}.");
        }

        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
            QuestShopConfiguration.Load(configuration.ToJson());
        }
    }

    internal static QuestShopStatePacket Open(Player player, Guid shopId, string message = "")
    {
        var shop = Current.Find(shopId);
        if (shop == null)
            return new QuestShopStatePacket(Guid.Empty, "Quest Shop", string.Empty, [], "This Quest Shop is not configured.");

        lock (Gate)
            OpenShopsByPlayer[player.Id] = shop.Id;

        return BuildState(player, shop, message);
    }

    internal static bool TryOffer(Player player, Guid shopId, Guid questId, out string error)
    {
        error = string.Empty;
        QuestShopDefinition? shop;
        lock (Gate)
        {
            if (!OpenShopsByPlayer.TryGetValue(player.Id, out var openShopId) || openShopId != shopId)
            {
                error = "Open this Quest Shop again before viewing a quest.";
                return false;
            }

            shop = Current.Find(openShopId);
        }

        if (shop == null || !(shop.QuestIds ?? []).Contains(questId))
        {
            error = "This quest is not offered here.";
            return false;
        }

        var quest = QuestDescriptor.Get(questId);
        if (quest == null)
        {
            error = "This quest no longer exists.";
            return false;
        }

        if (!player.CanStartQuest(quest))
        {
            error = "You do not currently meet this quest's requirements.";
            return false;
        }

        if (!player.QuestOffers.Contains(quest.Id))
            player.OfferQuest(quest);

        return true;
    }

    private static QuestShopStatePacket BuildState(Player player, QuestShopDefinition shop, string message)
    {
        var entries = (shop.QuestIds ?? [])
            .Select(QuestDescriptor.Get)
            .Where(quest => quest != null)
            .Select(quest =>
            {
                var inProgress = player.QuestInProgress(quest!.Id, QuestProgressState.OnAnyTask, Guid.Empty);
                var completed = player.QuestCompleted(quest.Id);
                var canStart = !inProgress && player.CanStartQuest(quest);

                var status = inProgress
                    ? "IN PROGRESS"
                    : canStart
                        ? "AVAILABLE"
                        : completed
                            ? "COMPLETED"
                            : "LOCKED";

                var description = !string.IsNullOrWhiteSpace(quest.StartDescription)
                    ? quest.StartDescription
                    : quest.BeforeDescription;

                return new QuestShopEntryPacket(
                    quest.Id,
                    quest.Name,
                    description ?? string.Empty,
                    status,
                    canStart,
                    QuestShopRequirementFormatter.Build(player, quest)
                );
            })
            .ToArray();

        return new QuestShopStatePacket(shop.Id, shop.Name, shop.Description, entries, message);
    }

    private static QuestShopConfiguration LoadCore()
    {
        try
        {
            var configuration = File.Exists(PathName)
                ? QuestShopConfiguration.FromJson(File.ReadAllText(PathName))
                : new QuestShopConfiguration();

            QuestShopConfiguration.Load(configuration.ToJson());
            return configuration;
        }
        catch
        {
            var empty = new QuestShopConfiguration();
            QuestShopConfiguration.Load(empty.ToJson());
            return empty;
        }
    }
}
