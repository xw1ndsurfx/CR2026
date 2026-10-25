using Intersect.Editor.General;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Render = Intersect.Editor.Core.Graphics;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmJoustConfiguration : Form
{
    private readonly JoustConfiguration _config;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, DisplayMember = nameof(JoustArena.Name) };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill };
    private readonly PictureBox _map = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 30 };
    private readonly Button _save = new() { AutoSize = true };
    private Guid _request;
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmJoustConfiguration(string json)
    {
        _config = JoustConfiguration.FromJson(json);
        Text = T("Jousting Editor"); Size = new(1280, 820); MinimumSize = new(900, 600);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        void Button(string name, Action action) { var b = new Button { Text = T(name), AutoSize = true }; b.Click += (_, _) => action(); tools.Controls.Add(b); }
        Button("New arena on current map", () => {
            if (Globals.CurrentMap == null) return;
            var arena = new JoustArena { MapId = Globals.CurrentMap.Id };
            _config.Arenas = [.._config.Arenas, arena]; RefreshList(); _list.SelectedItem = arena;
        });
        Button("Arena rules", () => { if (_list.SelectedItem is JoustArena a) _properties.SelectedObject = a.Rules; });
        Button("Arena settings", () => _properties.SelectedObject = _list.SelectedItem);
        var kinds = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        kinds.Items.AddRange(Enum.GetValues<JoustCinematicKind>().Select(k => (object)T(JoustCinematics.Title(k))).ToArray());
        kinds.SelectedIndex = 0; tools.Controls.Add(kinds);
        Button("Edit joust cinematic", () => {
            if (_list.SelectedItem is not JoustArena a || !a.IsValid || Globals.CurrentMap?.Id != a.MapId)
            { _status.Text = T("The jousting configuration is invalid."); return; }
            var kind = (JoustCinematicKind)kinds.SelectedIndex;
            new FrmCinematicDirector(JoustCinematics.Create(a, kind, T), id => {
                switch (kind) {
                    case JoustCinematicKind.Introduction: a.IntroductionEventId = id; break;
                    case JoustCinematicKind.Charge: a.ChargeEventId = id; break;
                    case JoustCinematicKind.Impact: a.ImpactEventId = id; break;
                    case JoustCinematicKind.Unhorse: a.UnhorseEventId = id; break;
                    case JoustCinematicKind.Victory: a.VictoryEventId = id; break;
                }
                _properties.Refresh();
            }).Show(this);
        });
        _save.Text = T("Save"); _save.Click += (_, _) => Save(); tools.Controls.Add(_save);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1200, 700), SplitterDistance = 210 };
        var right = new SplitContainer { Dock = DockStyle.Fill, Size = new(990, 700), SplitterDistance = 650 };
        split.Panel1.Controls.Add(_list); split.Panel2.Controls.Add(right); right.Panel1.Controls.Add(_map); right.Panel2.Controls.Add(_properties);
        Controls.Add(split); Controls.Add(tools); Controls.Add(_status);
        _list.SelectedIndexChanged += (_, _) => { _properties.SelectedObject = _list.SelectedItem; Preview(); };
        _properties.PropertyValueChanged += (_, _) => Preview();
        _map.MouseClick += (_, e) => {
            if (_list.SelectedItem is not JoustArena a || _map.Image is not { } image) return;
            var scale = Math.Min(_map.Width / (float)image.Width, _map.Height / (float)image.Height);
            var x = (int)((e.X - (_map.Width - image.Width * scale) / 2) / scale / Intersect.Options.Instance.Map.TileWidth);
            var y = (int)((e.Y - (_map.Height - image.Height * scale) / 2) / scale / Intersect.Options.Instance.Map.TileHeight);
            if (x < 0 || y < 0 || x >= Intersect.Options.Instance.Map.MapWidth || y >= Intersect.Options.Instance.Map.MapHeight) return;
            if ((ModifierKeys & Keys.Shift) != 0) a.OpponentY = y;
            else if (e.Button == MouseButtons.Right) a.EndX = x;
            else { a.StartX = x; a.StartY = a.EndY = y; }
            _properties.Refresh(); Preview();
        };
        Disposed += (_, _) => _map.Image?.Dispose();
        _status.Text = T("Left click: start. Right click: end. Shift-click: opponent lane."); RefreshList();
    }
    private void RefreshList() { _list.DataSource = null; _list.DataSource = _config.Arenas; }
    private void Preview()
    {
        if (_list.SelectedItem is not JoustArena a || Globals.CurrentMap?.Id != a.MapId) return;
        var image = Render.RenderCinematicMap(new CinematicScene { MapId = a.MapId }, 0);
        if (image == null) return;
        using (var g = System.Drawing.Graphics.FromImage(image))
        using (var pen = new Pen(System.Drawing.Color.Gold, 2))
        {
            var tw = Intersect.Options.Instance.Map.TileWidth; var th = Intersect.Options.Instance.Map.TileHeight;
            g.DrawLine(pen, (a.StartX + .5f) * tw, (a.StartY + .5f) * th, (a.EndX + .5f) * tw, (a.EndY + .5f) * th);
            foreach (var x in new[] { a.StartX, a.EndX }) g.DrawRectangle(pen, x * tw, a.StartY * th, tw, th);
            using var other = new Pen(System.Drawing.Color.DeepSkyBlue, 2);
            g.DrawLine(other, (a.StartX + .5f) * tw, (a.OpponentY + .5f) * th, (a.EndX + .5f) * tw, (a.OpponentY + .5f) * th);
            foreach (var x in new[] { a.StartX, a.EndX }) g.DrawRectangle(other, x * tw, a.OpponentY * th, tw, th);
            var middle = (a.StartX + a.EndX + 1) / 2f * tw;
            g.DrawLine(Pens.OrangeRed, middle, (Math.Min(a.StartY, a.OpponentY) - .5f) * th,
                middle, (Math.Max(a.StartY, a.OpponentY) + 1.5f) * th);
        }
        var previous = _map.Image; _map.Image = image; previous?.Dispose();
    }
    private void Save()
    {
        if (!_config.IsValid) { _status.Text = T("The jousting configuration is invalid."); return; }
        _request = Guid.NewGuid(); _save.Enabled = false;
        Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.SaveConfiguration, ConfigurationJson = _config.ToJson(), RequestId = _request });
    }
    internal void Response(JoustStatePacket packet)
    {
        if (packet.RequestId != _request) return;
        _save.Enabled = true; _status.Text = T(packet.Saved ? "Saved" : "The jousting configuration is invalid.");
    }
}
