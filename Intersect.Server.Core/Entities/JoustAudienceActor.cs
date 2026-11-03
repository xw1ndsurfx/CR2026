using Intersect.Enums;
using Intersect.GameObjects;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Network.Packets.Server;
namespace Intersect.Server.Entities;
/// <summary>Shared audience decoration: no AI, combat, loot or persistence.</summary>
internal sealed class JoustAudienceActor : Entity
{
    internal Guid DefinitionId { get; }
    internal JoustAudienceActor(NPCDescriptor npc, Guid map, Guid instance, int x, int y, int facing)
    {
        DefinitionId = npc.Id; Name = npc.Name; Sprite = npc.Sprite; Face = ""; MapId = map; MapInstanceId = instance;
        X = x; Y = y; Dir = (Direction)facing; HideName = true; Passable = false;
        BaseStats[(int)Intersect.Enums.Stat.Speed] = 20; SetMaxVital(Vital.Health, 1); SetVital(Vital.Health, 1);
    }
    public override bool CanAttack(Entity entity, SpellDescriptor spell) => false;
    public override void Update(long timeMs) { }
    protected override Intersect.Server.Framework.Items.EntityItemSource? AsItemSource() => null;
    public override EntityPacket EntityPacket(EntityPacket packet = null, Player forPlayer = null) =>
        base.EntityPacket(new NpcEntityPacket { NpcId = DefinitionId, Aggression = NpcAggression.Neutral, JoustSeated = true }, forPlayer);
}
