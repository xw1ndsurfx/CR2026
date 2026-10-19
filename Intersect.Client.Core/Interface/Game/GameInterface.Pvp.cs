using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Maps;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Pvp;
namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private PvpWindow? _pvpWindow; private CaptureChestState? _captureChest; private long _captureReceived;
    private ExperienceEventHud? _captureHud; private Button? _leaveCapture;
    private readonly ImagePanel?[] _chestIcons = [null, null]; private readonly Label?[] _chestLabels = [null, null];
    public void OpenPvp()
    {
        _pvpWindow ??= new PvpWindow(GameCanvas);
        _pvpWindow.Open();
    }
    public void ApplyPvp(PvpStatePacket packet)
    {
        _pvpWindow ??= new PvpWindow(GameCanvas);
        _pvpWindow.Apply(packet);
        _captureChest = packet.Match;
        _captureReceived = Environment.TickCount64;
        if (Globals.Me != null)
        {
            Globals.Me.PvpGrade = packet.Grade;
            Globals.Me.PvpPoints = packet.Points;
        }
    }
    private void UpdatePvpHud()
    {
        _captureHud ??= new ExperienceEventHud(GameCanvas, "CaptureChestHud");
        if (_leaveCapture == null)
        {
            _leaveCapture = new Button(GameCanvas, "LeaveCaptureChest") { Text = ClientLanguages.Text("Leave match"), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? GameCanvas.Skin.DefaultFont, FontSize = 10 };
            _leaveCapture.Clicked += (_, _) => PvpWindow.Send(PvpAction.Leave);
        }
        var state = _captureChest;
        _captureHud.IsHidden = _leaveCapture.IsHidden = state == null;
        for (var i = 0; i < 2; i++)
        {
            _chestIcons[i] ??= new ImagePanel(GameCanvas, "CaptureChestIcon" + i) { MouseInputEnabled = false, KeyboardInputEnabled = false, MaintainAspectRatio = true };
            _chestLabels[i] ??= new Label(GameCanvas, "CaptureChestLabel" + i) { Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? GameCanvas.Skin.DefaultFont, FontSize = 10, MouseInputEnabled = false };
            _chestIcons[i]!.IsHidden = _chestLabels[i]!.IsHidden = true;
        }
        if (state == null)
            return;
        var seconds = Math.Max(0, state.RemainingSeconds - (Environment.TickCount64 - _captureReceived) / 1000);
        var team = ClientLanguages.Text(state.Team == 0 ? "Red team" : "Blue team");
        var title = ClientLanguages.Format("Capture the Chest [{0}] - Red {1} : {2} Blue - {3}:{4}", team, state.RedScore, state.BlueScore, seconds / 60, (seconds % 60).ToString("00"));
        var respawn = Math.Max(0, state.RespawnSeconds - (Environment.TickCount64 - _captureReceived) / 1000);
        var hint = respawn > 0 ? ClientLanguages.Format("Respawning in {0}s", respawn) : ClientLanguages.Format("Bring the enemy chest home while your own is at base. First to {0} wins.", state.ScoreToWin);
        _captureHud.WrappingBehavior = Intersect.Client.Framework.Gwen.WrappingBehavior.Wrapped;
        _captureHud.ShowLine(title + "\n" + hint, 80);
        _captureHud.SetPosition(Math.Max(8, (GameCanvas.Width - 510) / 2), 80);
        _captureHud.SetSize(Math.Min(500, GameCanvas.Width - 130), 64);
        _leaveCapture.SetBounds(_captureHud.Right + 6, 80, 100, 30);
        _leaveCapture.BringToFront();
        if (Globals.Me?.MapId != state.MapId || MapInstance.Get(state.MapId) is not { } map)
            return;
        for (var i = 0; i < Math.Min(2, state.Chests.Length); i++)
        {
            var chest = state.Chests[i];
            var x = chest.X;
            var y = chest.Y;
            if (chest.Carrier != Guid.Empty && Globals.Entities.TryGetValue(chest.Carrier, out var carrier))
            {
                x = carrier.X;
                y = carrier.Y - .5f;
            }
            var zoom = Globals.Database.WorldZoom;
            var size = (int)Math.Clamp(32 * zoom, 20, 96);
            var sx = (int)((map.X + (x + .5f) * Options.Instance.Map.TileWidth - Graphics.CurrentView.Left) * zoom) - size / 2;
            var sy = (int)((map.Y + (y + 1) * Options.Instance.Map.TileHeight - Graphics.CurrentView.Top) * zoom) - size;
            var icon = _chestIcons[i]!;
            var label = _chestLabels[i]!;
            icon.Texture = GameContentManager.Current.GetTexture(TextureType.Item, ItemDescriptor.Get(state.ChestItemId)?.Icon ?? "");
            if (icon.Texture != null)
            {
                icon.SetBounds(sx, sy, size, size);
                icon.RenderColor = i == 0 ? new Color(255, 255, 155, 135) : new Color(255, 140, 185, 255);
                icon.IsHidden = false;
                icon.SendToBack();
            }
            else
            {
                label.Text = ClientLanguages.Text(i == 0 ? "Red chest" : "Blue chest");
                label.SetBounds(sx - 30, sy, 130, 26);
                label.TextColorOverride = i == 0 ? new Color(255, 255, 120, 100) : new Color(255, 100, 180, 255);
                label.IsHidden = false;
                label.SendToBack();
            }
        }
    }
}
