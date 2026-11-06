using Intersect.Client.Core;
using Intersect.Client.General;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private AdvancedCombatHud? _advancedCombatHud;
    private bool _advancedCombatEnabled;

    public void ReceiveAdvancedCombatState(AdvancedCombatStatePacket packet)
    {
        if (packet == null || Globals.Me?.Id != packet.PlayerId)
            return;

        _advancedCombatEnabled = packet.Enabled;
        _advancedCombatHud ??= new AdvancedCombatHud(GameCanvas);
        _advancedCombatHud.Apply(packet);
        if (!packet.Enabled || packet.Feedback == AdvancedCombatFeedback.None)
            return;

        var file = packet.Feedback switch
        {
            AdvancedCombatFeedback.Dodge => "advanced-combat/dodge.wav",
            AdvancedCombatFeedback.PerfectParry => "advanced-combat/perfect-parry.wav",
            AdvancedCombatFeedback.CounterAttack => "advanced-combat/counterattack.wav",
            AdvancedCombatFeedback.Exhausted => "advanced-combat/exhausted.wav",
            _ => null,
        };
        if (file != null)
            Audio.AddGameSound(file, false);
    }

    private void UpdateAdvancedCombatHud()
    {
        if (_advancedCombatHud is null)
            return;

        _advancedCombatHud.IsHidden = !_advancedCombatEnabled ||
            Globals.Me is null || SuppressStatusHuds;
        if (!_advancedCombatHud.IsHidden)
            _advancedCombatHud.Update(this);
    }
}
