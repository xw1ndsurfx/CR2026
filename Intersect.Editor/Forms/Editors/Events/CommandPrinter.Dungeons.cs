using Intersect.Editor.Maps;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(StartDungeonCommand command, MapInstance map)
    {
        var dungeon = DungeonConfiguration.Instance.Find(command.DungeonId);
        var dungeonName = dungeon == null ? "Unknown Dungeon" : $"[{dungeon.Rank}] {dungeon.Name}";
        var destination = MapDescriptor.Get(command.MapId)?.Name ?? "Unknown Map";
        var changeInstance = command.UseWarpSettings ? command.ChangeInstance : true;
        var instanceType = command.UseWarpSettings
            ? command.InstanceType
            : (command.UsePartyInstance ? MapInstanceType.Shared : MapInstanceType.Personal);
        var instance = changeInstance ? instanceType.ToString() : "Keep current instance";
        return $"Start Dungeon: {dungeonName} -> {destination} ({command.X},{command.Y}) {command.Direction} | {instance}";
    }
}
