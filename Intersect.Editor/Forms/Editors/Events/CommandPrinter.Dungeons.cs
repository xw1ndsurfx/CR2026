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
        var instance = command.UsePartyInstance ? "Party/Personal" : "Personal";
        return $"Start Dungeon: {dungeonName} -> {destination} ({command.X},{command.Y}) | {instance}";
    }
}
