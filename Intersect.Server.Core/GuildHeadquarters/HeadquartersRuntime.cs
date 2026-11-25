using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Network.Packets.GuildHeadquarters;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Newtonsoft.Json;

namespace Intersect.Server.GuildHeadquarters;

internal static class HeadquartersRuntime
{
    internal static readonly HeadquartersStore Store = new(Path.Combine("resources", "guild-headquarters.json"));
    private sealed record Admission(Player Player, Guild Guild, HeadquartersVisit Return, Guid Host, long Until);
    private static readonly ConcurrentDictionary<Guid, Admission> Visits = new();
    private static readonly ConcurrentDictionary<Guid, byte> Evictions = new();
    private static readonly HeadquartersInvitations Invitations = new();
    private static readonly object AdmissionGate = new();
    private static HeadquartersConfiguration Config => Store.Configuration;
    private static long Now => Environment.TickCount64;

    internal static void Validate(HeadquartersConfiguration config)
    {
        foreach (var tier in config.Tiers)
        {
            var map = MapController.Get(tier.MapId);
            if (map == null || !SafeMap(map) || !Walkable(map, tier.EntryX, tier.EntryY) || !Walkable(map, tier.BankX, tier.BankY) ||
                ClassDescriptor.Lookup.Values.OfType<ClassDescriptor>().Any(c => c.SpawnMapId == tier.MapId))
                throw new InvalidOperationException("Choose isolated maps with walkable entry and bank tiles.");
            if (tier.BankSlots > Options.Instance.Bank.MaxSlots)
                throw new InvalidOperationException("The bank capacity exceeds the server limit.");
            if (tier.CostItemId != Guid.Empty && (ItemDescriptor.Get(tier.CostItemId) is not {} item ||
                !item.CanGuildBank || (!item.Stackable && item.ItemType != ItemType.Currency)))
                throw new InvalidOperationException("Choose a stackable resource that can be deposited in the guild bank.");
        }
    }
    private static bool SafeMap(MapController map) => map.Up == Guid.Empty && map.Down == Guid.Empty && map.Left == Guid.Empty && map.Right == Guid.Empty &&
        !MapController.Lookup.Values.OfType<MapController>().Any(m => m.Up == map.Id || m.Down == map.Id || m.Left == map.Id || m.Right == map.Id);
    private static bool Walkable(MapController map, int x, int y) => x >= 0 && y >= 0 && x < Options.Instance.Map.MapWidth && y < Options.Instance.Map.MapHeight &&
        (map.Attributes == null || x < map.Attributes.GetLength(0) && y < map.Attributes.GetLength(1) &&
            (map.Attributes[x, y] == null || map.Attributes[x, y] is MapSoundAttribute or MapAnimationAttribute or MapNpcAvoidAttribute or MapCritterAttribute or MapDockAttribute or MapRainPuddleAttribute));
    private static bool Member(Player player, Guild guild) => player.GuildId == guild.Id && guild.Members.ContainsKey(player.Id) && Guild.Guilds.ContainsKey(guild.Id);
    private static bool Manager(Player player, Guild guild) => Member(player, guild) && player.GuildRank >= 0 && player.GuildRank <= Config.ManagingRank;
    private static bool Inviter(Player player, Guild guild) => Config.AllowVisitors && Member(player, guild) && player.GuildRank >= 0 && player.GuildRank <= Config.InvitingRank;
    private static HeadquartersTier? Tier(Guild guild)
    {
        try { lock (guild.Lock) return Config.Tiers.ElementAtOrDefault(HeadquartersProfile.Read(guild.HeadquartersJson).TierIndex(Config)); }
        catch { return null; }
    }
    internal static bool IsVisitor(Player player) => Visits.TryGetValue(player.Id, out var visit) && visit.Host != Guid.Empty && ReferenceEquals(visit.Player, player);
    internal static bool CanWarp(Player player, Guid map, Guid instance, MapInstanceType? type = null) => !Config.Tiers.Any(t => t.MapId == map) ||
        (type == null || type == MapInstanceType.Shared) && Config.Enabled && Visits.TryGetValue(player.Id, out var visit) && ReferenceEquals(visit.Player, player) &&
        visit.Return.InstanceId == instance && visit.Return.MapId == map && Authorized(visit);
    internal static bool ExitsPrivateInstance(Player player, Guid map) => Visits.TryGetValue(player.Id, out var visit) &&
        ReferenceEquals(visit.Player, player) && player.MapInstanceId == visit.Return.InstanceId &&
        map != visit.Return.MapId && !Config.Tiers.Any(t => t.MapId == map);
    private static bool Authorized(Admission visit)
    {
        if (!Config.Enabled || !Guild.Guilds.ContainsKey(visit.Guild.Id)) return false;
        if (visit.Host == Guid.Empty) return Member(visit.Player, visit.Guild);
        var host = Player.FindOnline(visit.Host);
        return Config.AllowVisitors && visit.Until > Now && host is { InGame: true } && Inviter(host, visit.Guild) &&
            !host.Blocks(visit.Player.Id) && !visit.Player.Blocks(host.Id);
    }
    internal static void Handle(Player player, HeadquartersRequestPacket request)
    {
        if (!player.InGame || player.Client == null) return;
        lock (player.EntityLock)
        {
            if (player.HeadquartersNextRequest > Now) return;
            player.HeadquartersNextRequest = Now + (request.Action == HeadquartersAction.Invite ? 3000 : 300);
            try
            {
                if (request.Action == HeadquartersAction.Leave) { Leave(player); return; }
                if (request.Action == HeadquartersAction.Open) { Send(player, true); return; }
                if (!Config.Enabled) throw new InvalidOperationException("Headquarters are not available yet.");
                if (request.Action is HeadquartersAction.AcceptInvitation or HeadquartersAction.DeclineInvitation)
                {
                    var invitation = Invitations.Take(request.InvitationId, player.Id, Now);
                    if (request.Action == HeadquartersAction.DeclineInvitation) return;
                    var host = invitation == null ? null : Player.FindOnline(invitation.Host);
                    if (invitation == null || host?.Guild is not {} hostingGuild || hostingGuild.Id != invitation.Guild ||
                        !Inviter(host, hostingGuild) || host.Blocks(player.Id) || player.Blocks(host.Id))
                        throw new InvalidOperationException("This invitation is no longer valid.");
                    Enter(player, hostingGuild, host.Id); return;
                }
                var guild = player.Guild;
                if (guild == null || !Member(player, guild) || IsVisitor(player)) throw new InvalidOperationException("Only guild members can use this service.");
                switch (request.Action)
                {
                    case HeadquartersAction.Enter: Enter(player, guild); return;
                    case HeadquartersAction.OpenBank:
                        if (!Inside(player, guild) || Tier(guild) is not {} tier || Math.Abs(player.X - tier.BankX) > 1 || Math.Abs(player.Y - tier.BankY) > 1)
                            throw new InvalidOperationException("Stand next to the guild bank location.");
                        Send(player, false, bankOpened: player.OpenBank(true)); return;
                    case HeadquartersAction.Upgrade:
                        if (!Inside(player, guild) || !Manager(player, guild)) throw new InvalidOperationException("Enter your headquarters with management permission first.");
                        if (player.NavalBusy) throw new InvalidOperationException("Finish your current activity first.");
                        lock (Store.Gate)
                        {
                            if (request.ConfigurationRevision != Store.Revision) throw new InvalidOperationException("The headquarters changed. Review the updated details.");
                            Validate(Config);
                            lock (guild.Lock)
                            {
                                if (!Manager(player, guild)) throw new InvalidOperationException("You cannot manage this headquarters.");
                                HeadquartersPurchase.Buy(guild, Config, request.TierId, request.Revision, player.GuildRank, guild.Save);
                            }
                        }
                        lock (guild.Lock)
                            foreach (var member in guild.FindOnlineMembers())
                                if (member.GuildBank) member.BankInterface?.SendOpenBank();
                        // Other players relocate and refresh their bank on their own update, under their own entity lock.
                        Send(player, true, "Headquarters upgraded."); return;
                    case HeadquartersAction.Invite:
                        if (!Inviter(player, guild) || !Inside(player, guild)) throw new InvalidOperationException("Enter your headquarters with invitation permission first.");
                        var guest = request.Name is { Length: > 0 and <= 64 } ? Player.FindOnline(request.Name.Trim()) : null;
                        if (guest == null || !guest.InGame || guest.Id == player.Id || guest.GuildId == guild.Id || guest.Blocks(player.Id) || player.Blocks(guest.Id))
                            throw new InvalidOperationException("Choose an online visitor who has not blocked you.");
                        var token = Invitations.Create(player.Id, guest.Id, guild.Id, Now);
                        if (token == Guid.Empty) throw new InvalidOperationException("Too many pending invitations. Try again later.");
                        guest.SendPacket(new HeadquartersInvitationPacket { Id = token, HostName = player.Name, GuildName = guild.Name,
                            ExpiresUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 120000, VisitMinutes = Config.VisitMinutes,
                            Sound = Config.Assets.InvitationSound, Icon = Config.Assets.InvitationIcon, Volume = Config.Assets.EffectVolume });
                        Send(player, true, "Invitation sent."); return;
                    case HeadquartersAction.EndVisits:
                        if (!Inviter(player, guild) || !Inside(player, guild)) throw new InvalidOperationException("You cannot manage this headquarters.");
                        Invitations.RemoveGuild(guild.Id);
                        foreach (var visit in Visits.Values.Where(v => v.Guild.Id == guild.Id && v.Host != Guid.Empty)) Evictions[visit.Player.Id] = 1;
                        Send(player, true, "Visitor invitations revoked."); return;
                    default: return;
                }
            }
            catch (Exception exception)
            {
                Send(player, true, exception is InvalidOperationException ? exception.Message : "Headquarters could not be saved. Please try again.");
            }
        }
    }
    private static bool Inside(Player player, Guild guild) => Visits.TryGetValue(player.Id, out var visit) && ReferenceEquals(visit.Player, player) &&
        visit.Guild.Id == guild.Id && player.MapId == visit.Return.MapId && player.MapInstanceId == visit.Return.InstanceId && Authorized(visit);

    private static void Enter(Player player, Guild guild, Guid host = default)
    {
        if (Visits.ContainsKey(player.Id) || player.InstanceType != MapInstanceType.Overworld || !player.CanUseLobbyTravel || player.NavalBusy ||
            player.NavalShipId != Guid.Empty || Intersect.Server.Dungeons.DungeonRunRuntime.IsPlayerInActiveRun(player))
            throw new InvalidOperationException("Finish your activity and return to the overworld first.");
        var tier = Tier(guild) ?? throw new InvalidOperationException("Headquarters are not available yet.");
        var map = MapController.Get(tier.MapId);
        if (map == null || !SafeMap(map) || !Walkable(map, tier.EntryX, tier.EntryY)) throw new InvalidOperationException("The headquarters map is unavailable.");
        var location = new HeadquartersVisit { GuildId = guild.Id, InstanceId = guild.GuildInstanceId, MapId = tier.MapId,
            ReturnMapId = player.MapId, ReturnX = (int)player.X, ReturnY = (int)player.Y };
        player.HeadquartersVisit = location;
        lock (AdmissionGate)
        {
            if (host != Guid.Empty && Visits.Values.Count(v => v.Guild.Id == guild.Id && v.Host != Guid.Empty) >= 20)
            { player.HeadquartersVisit = null; throw new InvalidOperationException("The headquarters visitor limit has been reached."); }
            Visits[player.Id] = new(player, guild, location, host, Now + Config.VisitMinutes * 60000L);
        }
        // Persist the return BEFORE teleporting, so even a crash in Warp cannot strand the player.
        try { if (player.User?.Save(force: true) != UserSaveResult.Completed) throw new IOException("Headquarters visit save failed."); }
        catch { Visits.TryRemove(player.Id, out _); player.HeadquartersVisit = null; throw; }
        player.AdminWarp(tier.MapId, tier.EntryX, tier.EntryY, guild.GuildInstanceId, MapInstanceType.Shared, true);
        if (player.MapId != tier.MapId || player.MapInstanceId != guild.GuildInstanceId)
        { Leave(player); throw new InvalidOperationException("The headquarters map is unavailable."); }
        Send(player, true);
    }
    internal static void Leave(Player player)
    {
        Visits.TryRemove(player.Id, out var visit); Evictions.TryRemove(player.Id, out _);
        var location = visit?.Return ?? ReadReturn(player);
        player.CloseBank();
        if (location != null)
        {
            var back = ReturnLocation(player, location);
            if (back != null) player.AdminWarp(back.ReturnMapId, back.ReturnX, back.ReturnY, Guid.Empty, MapInstanceType.Overworld, true);
            else player.WarpToSpawn(true);
        }
        player.HeadquartersVisit = null;
        player.User?.Save(force: true);
        Send(player, false);
    }
    private static HeadquartersVisit? ReadReturn(Player player)
    {
        try { return player.HeadquartersVisit; } catch { return null; }
    }
    private static bool ValidReturn(HeadquartersVisit location) => location.ReturnMapId != Guid.Empty && !Config.Tiers.Any(t => t.MapId == location.ReturnMapId) &&
        MapController.Get(location.ReturnMapId) is {} map && Walkable(map, location.ReturnX, location.ReturnY);
    private static HeadquartersVisit? ReturnLocation(Player player, HeadquartersVisit? preferred)
    {
        if (preferred != null && ValidReturn(preferred)) return preferred;
        var last = new HeadquartersVisit { ReturnMapId = player.LastOverworldMapId, ReturnX = player.LastOverworldX, ReturnY = player.LastOverworldY };
        if (ValidReturn(last)) return last;
        if (ClassDescriptor.Get(player.ClassId) is {} cls)
        {
            var spawn = new HeadquartersVisit { ReturnMapId = cls.SpawnMapId, ReturnX = cls.SpawnX, ReturnY = cls.SpawnY };
            if (ValidReturn(spawn)) return spawn;
        }
        foreach (var map in MapController.Lookup.Values.OfType<MapController>().Where(m => !Config.Tiers.Any(t => t.MapId == m.Id)))
            for (var y = 0; y < Options.Instance.Map.MapHeight; y++)
                for (var x = 0; x < Options.Instance.Map.MapWidth; x++)
                    if (Walkable(map, x, y)) return new() { ReturnMapId = map.Id, ReturnX = x, ReturnY = y };
        return null;
    }
    internal static void RestoreLogin(Player player)
    {
        if (player.GuildHeadquartersVisitJson == null && !Config.Tiers.Any(t => t.MapId == player.MapId)) return;
        var location = ReturnLocation(player, ReadReturn(player));
        if (location != null)
        {
            player.MapId = location.ReturnMapId; player.X = location.ReturnX; player.Y = location.ReturnY;
            player.MapInstanceId = Guid.Empty; player.InstanceType = MapInstanceType.Overworld;
        }
        else { player.MapInstanceId = Guid.Empty; player.InstanceType = MapInstanceType.Overworld; player.WarpToSpawn(true); }
        player.HeadquartersVisit = null;
    }
    internal static void Disconnect(Player player)
    { Visits.TryRemove(player.Id, out _); Evictions.TryRemove(player.Id, out _); Invitations.RemovePlayer(player.Id); }
    internal static void Update(Player player)
    {
        if (player.HeadquartersNextUpdate > Now) return;
        player.HeadquartersNextUpdate = Now + 1000;
        if (!Visits.TryGetValue(player.Id, out var visit)) return;
        if (player.MapInstanceId != visit.Return.InstanceId || player.MapId != visit.Return.MapId)
        { Visits.TryRemove(player.Id, out _); player.HeadquartersVisit = null; Send(player, false); return; }
        if (Evictions.TryRemove(player.Id, out _) || !Authorized(visit) || player.IsDead || Tier(visit.Guild) is not {} tier)
        { Leave(player); return; }
        if (MapController.Get(tier.MapId) is not {} currentMap || !SafeMap(currentMap)) { Leave(player); return; }
        if (visit.Return.MapId != tier.MapId)
        {
            if (MapController.Get(tier.MapId) is not {} map || !SafeMap(map) || !Walkable(map, tier.EntryX, tier.EntryY)) { Leave(player); return; }
            player.CloseBank();
            visit.Return.MapId = tier.MapId; player.HeadquartersVisit = visit.Return;
            player.AdminWarp(tier.MapId, tier.EntryX, tier.EntryY, visit.Return.InstanceId, MapInstanceType.Shared, true);
        }
        if (player.GuildBank && (Math.Abs(player.X - tier.BankX) > 1 || Math.Abs(player.Y - tier.BankY) > 1)) player.CloseBank();
        Send(player, false);
    }
    private static void Send(Player player, bool open, string message = "", bool bankOpened = false)
    {
        var config = Config;
        var inside = Visits.TryGetValue(player.Id, out var visit) && ReferenceEquals(visit.Player, player);
        var guild = inside ? visit!.Guild : player.Guild;
        var view = new HeadquartersView { Enabled = config.Enabled, Inside = inside, Visitor = inside && visit!.Host != Guid.Empty,
            Message = message, BankOpened = bankOpened, ConfigurationRevision = Store.Revision, VisitMinutes = config.VisitMinutes, Assets = config.Assets,
            MapId = inside ? visit!.Return.MapId : Guid.Empty, InstanceId = inside ? visit!.Return.InstanceId : Guid.Empty };
        if (guild != null)
        {
            lock (guild.Lock)
            {
                view.GuildId = guild.Id; view.GuildName = guild.Name; view.MemberCount = guild.Members.Count; view.BankSlots = guild.BankSlotsCount;
                view.CanManage = !view.Visitor && Manager(player, guild); view.CanInvite = !view.Visitor && Inviter(player, guild);
                view.Visitors = Visits.Values.Count(v => v.Guild.Id == guild.Id && v.Host != Guid.Empty);
                try
                {
                    var profile = HeadquartersProfile.Read(guild.HeadquartersJson);
                    var index = profile.TierIndex(config);
                    view.Revision = profile.Revision; view.TierNumber = index + 1;
                    view.Current = config.Tiers.ElementAtOrDefault(index); view.Next = config.Tiers.ElementAtOrDefault(index + 1);
                    view.NearBank = inside && !view.Visitor && view.Current is {} tier && Math.Abs(player.X - tier.BankX) <= 1 && Math.Abs(player.Y - tier.BankY) <= 1;
                    if (!view.Visitor && view.Next is {} next)
                    { view.AvailableQuantity = HeadquartersPurchase.Available(guild, next.CostItemId); view.CostItemName = ItemDescriptor.Get(next.CostItemId)?.Name ?? ""; }
                }
                catch { view.Enabled = false; view.Message = "Headquarters progress is unavailable. Contact an administrator."; }
            }
        }
        player.SendPacket(new HeadquartersStatePacket { Json = JsonConvert.SerializeObject(view), Open = open });
    }
}
