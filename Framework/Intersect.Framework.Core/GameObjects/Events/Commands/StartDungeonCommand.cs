using System.ComponentModel;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class StartDungeonCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.StartDungeon;

    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid DungeonId { get; set; }

    [DefaultValue(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
    public Guid MapId { get; set; }

    [DefaultValue((byte)0)]
    public byte X { get; set; }

    [DefaultValue((byte)0)]
    public byte Y { get; set; }

    [DefaultValue(true)]
    public bool UsePartyInstance { get; set; } = true;
}
