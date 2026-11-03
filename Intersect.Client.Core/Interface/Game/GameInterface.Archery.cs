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
    private void UpdateWeaponSkills()
    {
        if(General.Globals.Me==null) return;
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
            _weaponSkills??=new WeaponSkillBar(GameCanvas);_weaponSkills.Apply(packet);return;
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
                _weaponSkills??=new WeaponSkillBar(GameCanvas);_weaponSkills.Apply(packet);
            }
            if (_masteriesWindow == null && !packet.Open) return;
            _masteriesWindow ??= new WeaponMasteriesWindow(GameCanvas); _masteriesWindow.Apply(packet); return;
        }
        if (_archeryWindow == null && !packet.Open) return;
        _archeryWindow ??= new ArcheryWindow(GameCanvas); _archeryWindow.Apply(packet);
    }
    internal static void OpenWeaponMasteries() => Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.Masteries });
}
