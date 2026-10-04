using System.ComponentModel;
using Intersect.Framework.Core.GameObjects.Maps;

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

    /// <summary>
    /// Direction to face after entering the dungeon.
    /// </summary>
    public WarpDirection Direction { get; set; } = WarpDirection.Retain;

    /// <summary>
    /// True once the command has been saved with the Warp-style instance controls.
    /// Existing commands keep their legacy Personal/Party behavior until edited.
    /// </summary>
    [DefaultValue(false)]
    public bool UseWarpSettings { get; set; }

    /// <summary>
    /// Mirrors WarpCommand: when false, the warp keeps the player's current
    /// map-instance context.
    /// </summary>
    [DefaultValue(true)]
    public bool ChangeInstance { get; set; } = true;

    /// <summary>
    /// Requested instance type when ChangeInstance is enabled.
    /// </summary>
    public MapInstanceType InstanceType { get; set; } = MapInstanceType.Shared;

    /// <summary>
    /// Legacy setting kept so existing Start Dungeon event commands continue
    /// to behave exactly as before until they are edited and saved.
    /// </summary>
    [DefaultValue(true)]
    public bool UsePartyInstance { get; set; } = true;
}
