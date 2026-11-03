using Intersect.Network.Packets.Server;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    private const int MaximumIntoxicationDoses = 5;
    private long _intoxicatedUntil;
    private long _nextVomitAt;
    private long _nextIntoxicationDecayAt;
    private int _intoxicationDoses;
    private Guid _vomitAnimation;

    private void DrinkGrog(ItemDescriptor item)
    {
        var now = Environment.TickCount64;
        var durationSeconds = Math.Clamp(item.Consumable.Value == 0 ? 30 : item.Consumable.Value, 5, 120);
        if (now >= _intoxicatedUntil)
        {
            _intoxicationDoses = 0;
        }

        _intoxicationDoses = Math.Min(MaximumIntoxicationDoses, _intoxicationDoses + 1);
        _intoxicatedUntil = now + durationSeconds * 1000L;
        _nextIntoxicationDecayAt = now + 15000;
        _nextVomitAt = now + 5000;
        _vomitAnimation = item.AnimationId;
        SendIntoxicationState();
    }

    private void SendIntoxicationState()
    {
        var now = Environment.TickCount64;
        // Match the HUD countdown to the earliest of full expiration and
        // progressive dose recovery, rather than showing a stale end time.
        var recoveryUntil = _nextIntoxicationDecayAt + Math.Max(0, _intoxicationDoses - 1) * 15000L;
        var remainingMs = _intoxicationDoses > 0
            ? (int)Math.Clamp(Math.Min(_intoxicatedUntil, recoveryUntil) - now, 0L, 120000L)
            : 0;
        SendPacket(new IntoxicationPacket
        {
            PlayerId = Id,
            GreenMilliseconds = remainingMs,
            Intensity = Math.Clamp(_intoxicationDoses - 1, 0, 4),
        });
    }

    private void UpdateIntoxication()
    {
        var now = Environment.TickCount64;
        if (_intoxicationDoses <= 0)
        {
            return;
        }

        if (now >= _intoxicatedUntil)
        {
            _intoxicationDoses = 0;
            SendIntoxicationState();
            return;
        }

        if (now >= _nextIntoxicationDecayAt)
        {
            _intoxicationDoses = Math.Max(0, _intoxicationDoses - 1);
            _nextIntoxicationDecayAt = now + 15000;
            SendIntoxicationState();
            if (_intoxicationDoses == 0)
            {
                _intoxicatedUntil = now;
                return;
            }
        }

        // Vomiting only starts after at least four closely spaced drinks.
        if (_intoxicationDoses < 4 || now < _nextVomitAt || IsDead || !InGame)
        {
            return;
        }

        _nextVomitAt = now + 8000;
        if (_vomitAnimation != Guid.Empty)
        {
            PacketSender.SendAnimationToProximity(_vomitAnimation, 1, Id, MapId, 0, 0, Dir, MapInstanceId);
        }

        if (!Intersect.Server.Maps.MapController.TryGetInstanceFromMap(MapId, MapInstanceId, out var instance))
        {
            return;
        }

        foreach (var player in instance.GetPlayers())
        {
            if (player.MapInstanceId != MapInstanceId || player.MapId != MapId)
            {
                continue;
            }

            var splash = player.Id == Id
                ? 0
                : Math.Abs(player.X - X) <= 2 && Math.Abs(player.Y - Y) <= 2 ? 3500 : 0;
            player.SendPacket(new IntoxicationPacket
            {
                PlayerId = Id,
                GreenMilliseconds = splash,
                Vomit = true,
            });
        }
    }
}
