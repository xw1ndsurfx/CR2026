using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Metalworking;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Metalworking;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Intersect.Server.Professions;
using Intersect.Server.WorldEvents;
namespace Intersect.Server.MiniGames.Metalworking;

internal static class MetalworkRuntime
{
    private sealed class Session
    {
        public Guid Id = Guid.NewGuid(); public required Player Player; public MetalworkKind Kind; public Guid Map, Instance; public float X, Y;
        public Guid Recipe; public MetalworkRecipe? Definition; public MetalworkRound? Round; public bool Paid, Attempted; public long LastRequest, LastSeen, Actions, Sequence; public string Message = "", Sound = "";
    }
    private static readonly object Gate = new(); private static readonly Dictionary<Guid, Session> Sessions = []; private static long _nextTick;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> Active = new();
    internal static bool Contains(Guid id) => Active.ContainsKey(id);
    internal static bool Open(Player p, MetalworkKind kind)
    {
        if (p.Client is not { IsEditor: false } || !p.CanUseLobbyTravel)
            return false;
        lock (Gate)
        {
            var s = new Session { Player = p, Kind = kind, Map = p.MapId, Instance = p.MapInstanceId, X = p.X, Y = p.Y, LastSeen = Timing.Global.Milliseconds };
            Sessions[p.Id] = s;
            Active[p.Id] = 1;
            p.SendPacket(State(s));
            return true;
        }
    }
    private static MetalworkRecipe[] Recipes(Session s) => WorldActivitiesRuntime.Current.Metalworking.Recipes.Where(r => r.Enabled && r.Kind == s.Kind).ToArray();
    private static bool Materials(Player p, MetalworkRecipe r) => r.Ingredients.All(i => p.Items.Select((s, index) => (s, index)).Where(v => v.s.ItemId == i.ItemId && !p.SlotIsEquipped(v.index, out _) && v.s.BagId.GetValueOrDefault() == Guid.Empty).Sum(v => (long)v.s.Quantity) >= i.Quantity);
    internal static void Handle(Player p, MetalworkRequestPacket request)
    {
        lock (p.EntityLock)
            lock (Gate)
            {
                if (!Sessions.TryGetValue(p.Id, out var s) || s.Id != request.SessionId || request.RequestId <= s.LastRequest || !Enum.IsDefined(request.Action))
                    return;
                s.LastRequest = request.RequestId;
                var now = Timing.Global.Milliseconds;
                s.LastSeen = now;
                s.Message = "";
                s.Sound = "";
                if (request.Action == MetalworkAction.Leave)
                {
                    Close(s);
                    return;
                }
                if (!Valid(s))
                {
                    Close(s);
                    return;
                }
                switch (request.Action)
                {
                    case MetalworkAction.SelectRecipe:
                        if (s.Round == null || s.Round.Complete)
                        {
                            s.Recipe = Recipes(s).FirstOrDefault(r => r.Id == request.RecipeId)?.Id ?? Guid.Empty;
                            s.Round = null;
                            s.Paid = false;
                        }
                        break;
                    case MetalworkAction.Start:
                        var recipe = Recipes(s).FirstOrDefault(r => r.Id == s.Recipe);
                        if (recipe == null)
                            s.Message = "Choose a metalworking recipe.";
                        else if (!Materials(p, recipe))
                            s.Message = "Required materials must be in your main inventory.";
                        else if (recipe.ProfessionId != Guid.Empty && Math.Max(1, ProfessionRuntime.GetLevel(p, recipe.ProfessionId)) < recipe.RequiredLevel)
                            s.Message = "Your profession level is too low for this recipe.";
                        else if (s.Round == null || s.Round.Complete)
                        {
                            s.Definition = Newtonsoft.Json.JsonConvert.DeserializeObject<MetalworkRecipe>(Newtonsoft.Json.JsonConvert.SerializeObject(recipe));
                            s.Round = new MetalworkRound(s.Kind, now, Random.Shared.Next(25, 76));
                            s.Paid = false;
                            s.Attempted = false;
                            s.Message = "Keep the heat in the green zone with the bellows.";
                        }
                        break;
                    case MetalworkAction.Pump:
                    case MetalworkAction.Strike:
                        if (s.Round?.Act(request.Action, now) == true)
                        {
                            s.Actions++;
                            s.Sound = request.Action == MetalworkAction.Pump ? s.Definition?.PumpSound ?? "" : s.Definition?.StrikeSound ?? "";
                        }
                        break;
                    case MetalworkAction.Collect:
                        Collect(s);
                        break;
                }
                s.Round?.Tick(now);
                if (s.Round is { Complete: true } && !s.Paid && !s.Attempted)
                    Collect(s);
                p.SendPacket(State(s));
            }
    }
    private static bool Valid(Session s) => s.Player.InGame && !s.Player.IsDead && !s.Player.HasPendingDeath && s.Player.MapId == s.Map && s.Player.MapInstanceId == s.Instance && Math.Abs(s.Player.X - s.X) <= 3 && Math.Abs(s.Player.Y - s.Y) <= 3;
    private static void Collect(Session s)
    {
        if (s.Round is not { Complete: true } round || s.Paid || s.Definition is not { } recipe)
            return;
        s.Attempted = true;
        if (round.TimedOut || round.Quality < recipe.RequiredQuality)
        {
            s.Message = "The work failed. Your materials were not consumed.";
            return;
        }
        lock (s.Player.EntityLock)
        {
            if (!TryCraft(s.Player, recipe))
            {
                s.Message = "Crafting could not be saved. Check materials and inventory space, then collect again.";
                return;
            }
            s.Paid = true;
            PacketSender.SendInventory(s.Player);
            s.Player.NotifyMetalworkInventoryChanged(recipe);
            s.Sound = recipe.SuccessSound;
            s.Actions++;
            s.Message = "Craft completed! Your reward is in your inventory.";
            if (recipe.ProfessionId != Guid.Empty)
                ProfessionRuntime.AwardActivity(s.Player, recipe.ProfessionId, recipe.ProfessionExperience);
            s.Player.SendPacket(new ItemChangeNotificationPacket(recipe.OutputItemId, recipe.OutputQuantity));
        }
    }
    internal static bool TryCraft(Player player, MetalworkRecipe recipe)
    {
        var user = player.User;
        if (user == null)
            return false;
        lock (user.PokerSaveGate)
            return CommitCraft(player, recipe, () => user.Save(force: true));
    }
    // Keep inventory changes and the durable account checkpoint together. A failed
    // checkpoint restores the exact inventory; notifications run only after commit.
    private static bool CommitCraft(Player player, MetalworkRecipe recipe, Func<UserSaveResult> checkpoint)
    {
        if (!Materials(player, recipe) || ItemDescriptor.Get(recipe.OutputItemId) is not { } output)
            return false;
        {
            var before = player.Items.Select(slot => slot.Clone()).ToArray();
            var plan = before.Select(item => item.Clone()).ToArray();
            foreach (var ingredient in recipe.Ingredients)
            {
                var remaining = ingredient.Quantity;
                for (var i = 0; i < plan.Length && remaining > 0; i++)
                {
                    if (plan[i].ItemId != ingredient.ItemId || player.SlotIsEquipped(i, out _) || plan[i].BagId.GetValueOrDefault() != Guid.Empty)
                        continue;
                    var count = Math.Min(remaining, plan[i].Quantity);
                    plan[i].Quantity -= count;
                    remaining -= count;
                    if (plan[i].Quantity == 0)
                        plan[i] = Item.None;
                }
                if (remaining > 0)
                    return false;
            }
            var amount = recipe.OutputQuantity;
            var stackable = output.Stackable || output.ItemType == ItemType.Currency;
            var max = stackable ? Math.Max(1, output.MaxInventoryStack) : 1;
            for (var i = 0; i < plan.Length && amount > 0; i++)
            {
                if (!stackable || plan[i].ItemId != output.Id || plan[i].Quantity >= max || plan[i].BagId.GetValueOrDefault() != Guid.Empty)
                    continue;
                var count = Math.Min(amount, max - plan[i].Quantity);
                plan[i].Quantity += count;
                amount -= count;
            }
            for (var i = 0; i < plan.Length && amount > 0; i++)
            if (plan[i].ItemId == Guid.Empty)
            {
                var count = Math.Min(amount, max);
                plan[i] = new Item(output.Id, count);
                amount -= count;
            }
            if (amount > 0)
                return false;
            try
            {
                for (var i = 0; i < plan.Length; i++)
                    player.Items[i].Set(plan[i]);
                if (checkpoint() != UserSaveResult.Completed)
                    throw new InvalidOperationException("Craft checkpoint failed.");
            }
            catch { for (var i = 0; i < before.Length; i++) player.Items[i].Set(before[i]); PacketSender.SendInventory(player); return false; }
            return true;
        }
    }
    private static MetalworkStatePacket State(Session s)
    {
        var round = s.Round;
        return new MetalworkStatePacket { SessionId = s.Id, Sequence = ++s.Sequence, Kind = s.Kind, RecipeId = s.Recipe, Running = round != null, Stage = round?.Stage ?? MetalworkStage.Heating, Heat = round?.Heat ?? 25, Target = round?.Target ?? 50, Hits = round?.Hits ?? 0, Quality = round?.Quality ?? 0, LastScore = round?.LastScore ?? 0, StageElapsedMs = round == null ? 0 : (int)(Timing.Global.Milliseconds - round.StageStarted), Paid = s.Paid, Successful = round is { Complete: true, TimedOut: false } && s.Definition != null && round.Quality >= s.Definition.RequiredQuality, Message = s.Message, ActionNumber = s.Actions, Sound = s.Sound };
    }
    private static void Close(Session s)
    {
        Sessions.Remove(s.Player.Id);
        Active.TryRemove(s.Player.Id, out _);
        s.Player.SendPacket(new MetalworkStatePacket { SessionId = s.Id, Sequence = ++s.Sequence, Closed = true });
    }
    internal static bool Leave(Player p)
    {
        lock (Gate)
        {
            if (!Sessions.TryGetValue(p.Id, out var s))
                return false;
            Close(s);
            return true;
        }
    }
    internal static void Update(long now)
    {
        if (now < _nextTick)
            return;
        _nextTick = now + 200;
        Session[] sessions;
        lock (Gate)
            sessions = Sessions.Values.ToArray();
        foreach (var s in sessions)
            lock (s.Player.EntityLock)
                lock (Gate)
                {
                    if (!Sessions.TryGetValue(s.Player.Id, out var current) || !ReferenceEquals(current, s))
                        continue;
                    if (!Valid(s) || now - s.LastSeen > 120000)
                    {
                        Close(s);
                        continue;
                    }
                    s.Round?.Tick(now);
                    if (s.Round is { Complete: true } && !s.Paid && !s.Attempted)
                        Collect(s);
                    s.Player.SendPacket(State(s));
                }
    }
}
