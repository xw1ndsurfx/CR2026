using Intersect.Framework.Core;
using Intersect.Network.Packets.Guilds;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Newtonsoft.Json;
using System.Collections.Concurrent;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    private sealed class GuildEmblemRecord
    {
        public Guid GuildId { get; set; }
        public string GuildName { get; set; } = "";
        public Guid SubmittedBy { get; set; }
        public Guid Revision { get; set; }
        public Guid ReviewedBy { get; set; }
        public byte[] Approved { get; set; } = [];
        public byte[] Pending { get; set; } = [];
    }
    private static readonly object EmblemGate = new();
    private static readonly ConcurrentDictionary<Guid,long> EmblemRequests = new();
    private static readonly ConcurrentDictionary<Guid,long> EmblemReads = new();
    private static string EmblemPath => Path.Combine("resources", "guild-emblems.json");
    private static List<GuildEmblemRecord> LoadEmblems() => File.Exists(EmblemPath)
        ? JsonConvert.DeserializeObject<List<GuildEmblemRecord>>(File.ReadAllText(EmblemPath)) ?? [] : [];
    private static GuildEmblemEntry EmblemEntry(GuildEmblemRecord value, bool pending) => new()
    { GuildId = value.GuildId, GuildName = value.GuildName, Revision = value.Revision, Image = pending ? value.Pending : value.Approved };
    public void HandlePacket(Client client, GuildEmblemRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not {} player || !player.InGame || !Enum.IsDefined(packet.Action) || packet.GuildName == null || packet.GuildName.Length > 100 || packet.Image == null || packet.Image.Length > GuildEmblemPng.MaximumBytes) return;
        var now = Environment.TickCount64;
        var requests=packet.Action==GuildEmblemAction.Read?EmblemReads:EmblemRequests;
        var last=requests.GetValueOrDefault(player.Id);
        if(now-last<(packet.Action==GuildEmblemAction.Read?500:250) || !requests.TryUpdate(player.Id,now,last) && !requests.TryAdd(player.Id,now)) return;
        var moderator = player.Power.IsAdmin;
        var submitter = (player.Power.IsModerator || player.Power.IsAdmin) && LogiCoinPurchaseRuntime.HasActivePremium(player) && player.Guild != null;
        var response = new GuildEmblemStatePacket { CanSubmit = submitter, CanModerate = moderator, OpenWindow = packet.Action != GuildEmblemAction.Read, Page = Math.Clamp(packet.Page, 0, 10000) };
        GuildEmblemEntry? broadcast = null;
        try
        {
            lock (EmblemGate)
            {
                var records = LoadEmblems();
                if (packet.Action == GuildEmblemAction.Read)
                {
                    var guild=Intersect.Server.Database.PlayerData.Players.Guild.GetGuild(packet.GuildName);
                    var record = guild==null?null:records.FirstOrDefault(r => r.GuildId==guild.Id);
                    var entry=record==null?new GuildEmblemEntry():EmblemEntry(record,false);
                    entry.GuildName=packet.GuildName;
                    response.Approved=[entry];
                }
                else
                {
                    var changed = false;
                    if (packet.Action == GuildEmblemAction.Submit)
                    {
                        if (!submitter) throw new InvalidOperationException("Premium Game Master staff access is required.");
                        var image = GuildEmblemPng.Sanitize(packet.Image);
                        var record = records.FirstOrDefault(r => r.GuildId == player.Guild!.Id);
                        if (record == null) records.Add(record = new() { GuildId = player.Guild!.Id });
                        record.GuildName = player.Guild!.Name; record.Pending = image; record.Revision = Guid.NewGuid(); record.SubmittedBy = player.Id;
                        response.Message = "Guild emblem submitted for administrator approval."; changed = true;
                    }
                    else if (packet.Action is GuildEmblemAction.Approve or GuildEmblemAction.Reject or GuildEmblemAction.Remove)
                    {
                        if (!moderator) return;
                        var record = records.FirstOrDefault(r => r.GuildId == packet.GuildId && r.Revision == packet.Revision);
                        if (record == null) throw new InvalidOperationException("This submission has changed. Refresh the list.");
                        if (packet.Action != GuildEmblemAction.Remove && record.Pending.Length == 0) return;
                        record.GuildName=Intersect.Server.Database.PlayerData.Players.Guild.GetGuild(record.GuildId)?.Name??record.GuildName;
                        if (packet.Action == GuildEmblemAction.Approve) record.Approved = GuildEmblemPng.Sanitize(record.Pending);
                        if (packet.Action == GuildEmblemAction.Remove) record.Approved = [];
                        record.Pending = []; record.ReviewedBy = player.Id;
                        if (packet.Action != GuildEmblemAction.Reject) broadcast = EmblemEntry(record, false);
                        changed = true;
                    }
                    if (changed)
                    {
                        Directory.CreateDirectory("resources"); File.WriteAllText(EmblemPath + ".tmp", JsonConvert.SerializeObject(records)); File.Move(EmblemPath + ".tmp", EmblemPath, true);
                    }
                    var selected = records.Where(r => moderator || r.GuildId == player.Guild?.Id).OrderBy(r => r.GuildName).Skip(response.Page * 12).Take(13).ToArray();
                    response.HasMore = selected.Length > 12;
                    response.Approved = selected.Take(12).Select(r => EmblemEntry(r, false)).ToArray();
                    response.Pending = selected.Take(12).Where(r => r.Pending.Length > 0 && (moderator || submitter && r.GuildId == player.Guild?.Id)).Select(r => EmblemEntry(r, true)).ToArray();
                }
            }
            client.Send(response);
            if (broadcast != null)
                foreach (var online in Player.OnlinePlayers) online.SendPacket(new GuildEmblemStatePacket { Approved = [broadcast] });
        }
        catch (Exception error)
        {
            response.Message = error is InvalidOperationException ? error.Message : error is InvalidDataException or EndOfStreamException ? "Choose a valid RGB or RGBA PNG, at most 128 x 128 pixels and 64 KB." : "Guild emblem could not be saved. Try again.";
            client.Send(response);
        }
    }
}
