using Intersect.Client.Core;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.NPCs;

namespace Intersect.Client.Entities;

public partial class Entity
{
    private readonly NpcSoundTrigger _npcSoundTrigger = new();
    private long _nextNpcSoundCheck;

    private void UpdateNpcSounds()
    {
        if (Type != EntityType.GlobalEntity || Globals.Me is not { } player || Z != player.Z) return;
        var now = Environment.TickCount64;
        if (now < _nextNpcSoundCheck) return;
        _nextNpcSoundCheck = now + 100;
        var descriptor = NPCDescriptor.Get(NpcDescriptorId);
        if (descriptor == null || !SafeNpcSound(descriptor.ProximitySound) || descriptor.ProximitySoundRadius <= 0) return;
        var myMap = LatestMap;
        var playerMap = Maps.MapInstance.Get(player.MapId);
        if (myMap == null || playerMap == null || (MapId != player.MapId && (!Globals.GridMaps.ContainsKey(MapId) || !Globals.GridMaps.ContainsKey(player.MapId)))) return;
        var dx = (X + myMap.GridX * MapWidth) - (player.X + playerMap.GridX * MapWidth);
        var dy = (Y + myMap.GridY * MapHeight) - (player.Y + playerMap.GridY * MapHeight);
        var radius = Math.Clamp(descriptor.ProximitySoundRadius, 1, 100);
        if (_npcSoundTrigger.Approach(Math.Sqrt((double)dx * dx + (double)dy * dy), radius, now))
            Audio.AddMapSound(descriptor.ProximitySound, X, Y, MapId, false, 0, radius + 2, this);
    }

    internal void PlayNpcDeathSound()
    {
        if (Type != EntityType.GlobalEntity || !_npcSoundTrigger.Death()) return;
        var descriptor = NPCDescriptor.Get(NpcDescriptorId);
        if (descriptor != null && SafeNpcSound(descriptor.DeathSound))
            Audio.AddMapSound(descriptor.DeathSound, X, Y, MapId, false, 0, 20);
    }

    private static bool SafeNpcSound(string? sound) => !string.IsNullOrWhiteSpace(sound) &&
        sound.IndexOfAny(['/', '\\', ':']) < 0 && sound.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
}
