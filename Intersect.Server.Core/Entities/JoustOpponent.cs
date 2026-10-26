using Intersect.Enums;
using Intersect.Framework.Core.Mounts;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Entities;

/// <summary>A temporary, shared arena actor. No NPC AI, loot, XP, persistence or combat actions.</summary>
internal sealed class JoustOpponent : Entity
{
    internal MountDefinition Mount { get; }
    internal int StepMilliseconds { get; set; }
    public override int ScriptedMovementMilliseconds => StepMilliseconds;
    internal JoustOpponent(MountDefinition mount, Guid map, Guid instance, int x, int y)
    {
        Mount = mount; MapId = map; MapInstanceId = instance; X = x; Y = y;
        Name = mount.Name; Sprite = ""; Face = ""; HideName = true; Passable = false;
        BaseStats[(int)Intersect.Enums.Stat.Speed] = 20;
        SetMaxVital(Vital.Health, 1); SetVital(Vital.Health, 1);
    }
    public override float GetMovementTime() => Math.Max(1, StepMilliseconds);
    public override bool CanAttack(Entity entity, SpellDescriptor spell) => false;
    public override void Update(long timeMs) { /* Only the owning authoritative session moves this actor. */ }
    protected override Intersect.Server.Framework.Items.EntityItemSource? AsItemSource() => null;
    public override EntityPacket EntityPacket(EntityPacket packet = null, Player forPlayer = null) =>
        base.EntityPacket(new NpcEntityPacket { NpcId = Guid.Empty, Aggression = NpcAggression.Neutral,
            MountJson = new MountConfiguration { Mounts = [Mount] }.ToJson() }, forPlayer);
}
