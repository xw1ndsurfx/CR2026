using System.Text.Json;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Archery;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private ArcheryWindow? _archeryWindow;
    private WeaponMasteriesWindow? _masteriesWindow;
    private UniversalAmmunitionWindow? _ammunitionWindow;
    private WeaponSkillBar? _weaponSkills;
    private long _weaponSkillsRequestAt;
    internal bool HasActiveMiniGame =>
        _archeryWindow?.IsVisibleInTree == true || _fishingWindow?.IsVisibleInTree == true ||
        _exportationWindow?.IsVisibleInTree == true || _cookingWindow?.IsVisibleInTree == true ||
        _pokerWindow?.IsVisibleInTree == true || _blackjackWindow?.IsVisibleInTree == true ||
        _potionWindow?.IsVisibleInTree == true || _rouletteWindow?.IsVisibleInTree == true ||
        _diceWindow?.IsVisibleInTree == true || _lockpickingWindow?.IsVisibleInTree == true ||
        _metalworkWindow?.IsVisibleInTree == true || _joustWindow?.IsVisibleInTree == true ||
        _joustDuelWindow?.IsVisibleInTree == true || _joustSpectatorWindow?.IsVisibleInTree == true;

    internal bool CanShowWeaponSkills => WeaponSkillHudVisibility.Allowed(
        General.Globals.Me != null,
        General.Globals.Me is { } player && (Naval.NavalWorld.IsAboard(player.Id) || Naval.NavalWorld.OnBoardingDeck),
        HasActiveMiniGame,
        SuppressStatusHuds || Interface.InputBlockingComponents.Any(control => control.IsVisibleInTree && control.IsBlockingInput));

    private void UpdateWeaponSkills()
    {
        if (!CanShowWeaponSkills) { _weaponSkills?.Hide(); return; }
        if(_weaponSkills?.HasMasteryState!=true && Environment.TickCount64>=_weaponSkillsRequestAt)
        {
            _weaponSkillsRequestAt=Environment.TickCount64+3000;
            Networking.Network.SendPacket(new ArcheryRequestPacket { Action=ArcheryAction.SyncMasteries });
        }
        _weaponSkills?.Update();
    }
    internal void ApplyArcheryState(ArcheryStatePacket packet)
    {
        if(packet.WeaponSkillCooldownsJson.Length>0)
        {
            _weaponSkills??=new WeaponSkillBar(GameCanvas, () => CanShowWeaponSkills);_weaponSkills.Apply(packet);return;
        }
        if (packet.AmmunitionJson.Length > 0)
        {
            if (_ammunitionWindow == null && !packet.Open) return;
            _ammunitionWindow ??= new UniversalAmmunitionWindow(GameCanvas); _ammunitionWindow.Apply(packet); return;
        }
        if (packet.MasteriesJson.Length > 0 || packet.MasteriesReadOnly)
        {
            if(!packet.MasteriesReadOnly)
            {
                _weaponSkills??=new WeaponSkillBar(GameCanvas, () => CanShowWeaponSkills);_weaponSkills.Apply(packet);
            }
            if (_masteriesWindow == null && !packet.Open) return;
            _masteriesWindow ??= new WeaponMasteriesWindow(GameCanvas); _masteriesWindow.Apply(packet); return;
        }
        if (_archeryWindow == null && !packet.Open) return;
        _archeryWindow ??= new ArcheryWindow(GameCanvas); _archeryWindow.Apply(packet);
    }
    internal static void OpenWeaponMasteries() => Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.Masteries });
}
