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
    private sealed class MasteriesWindow(Canvas canvas) : Window(canvas, ClientLanguages.Text("Weapon Masteries"), false, "WeaponMasteriesWindow")
    {
        internal readonly ScrollControl Rows = new(canvas) { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        protected override void EnsureInitialized() { }
    }
    private ArcheryWindow? _archeryWindow;
    private MasteriesWindow? _masteriesWindow;
    private readonly List<Label> _masteryRows = [];
    internal void ApplyArcheryState(ArcheryStatePacket packet)
    {
        if (packet.MasteriesJson.Length > 0)
        {
            if (_masteriesWindow == null && !packet.Open) return;
            _masteriesWindow ??= new MasteriesWindow(GameCanvas) { DeleteOnClose = false };
            _masteriesWindow.SetSize(560, 390); _masteriesWindow.Alignment = [Alignments.Center];
            _masteriesWindow.Rows.Parent = _masteriesWindow;
            _masteriesWindow.Rows.SetBounds(12, 42, 536, 310);
            foreach (var row in _masteryRows) row.Dispose(); _masteryRows.Clear();
            var configuration = RoyalCombatConfiguration.FromJson(packet.ConfigurationJson);
            var experience = JsonSerializer.Deserialize<Dictionary<string, long>>(packet.MasteriesJson) ?? [];
            foreach (var mastery in configuration.Masteries)
            {
                var total = experience.GetValueOrDefault(mastery.Key); var level = mastery.Level(total);
                var xp = mastery.WithinLevel(total); var required = level == mastery.Cap ? 0 : mastery.Required(level);
                var label = new Label(_masteriesWindow.Rows) { Font = GameContentManager.Current.GetFont("sourcesanspro"), FontSize = 11,
                    AutoSizeToContents = false, TextColorOverride = Color.White,
                    Text = ClientLanguages.Format("{0} · Level {1}/{2} · XP {3}/{4} · {5}%",
                        ClientLanguages.ContentText(ClientLanguages.Text(mastery.Name)), level, mastery.Cap, xp, required,
                        required == 0 ? 100 : (int)(xp * 100 / required)) };
                label.SetBounds(4, _masteryRows.Count * 34, 510, 30); _masteryRows.Add(label);
            }
            if (packet.Open) { _masteriesWindow.Show(); _masteriesWindow.BringToFront(); } return;
        }
        if (_archeryWindow == null && !packet.Open) return;
        _archeryWindow ??= new ArcheryWindow(GameCanvas); _archeryWindow.Apply(packet);
    }
    internal static void OpenWeaponMasteries() => Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.Masteries });
}
