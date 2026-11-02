using Intersect.Client.Core;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.Guilds;
namespace Intersect.Client.Interface.Game;

internal static class GuildEmblems
{
    private static readonly Dictionary<string, IGameTexture?> Textures = new(StringComparer.Ordinal);
    private static readonly Queue<string> Requests = new();
    private static readonly Dictionary<string,long> RefreshAt = new(StringComparer.Ordinal);
    private static long _requestAt;
    internal static IGameTexture? Get(string name)
    {
        Textures.TryGetValue(name,out var texture);
        if (!RefreshAt.TryGetValue(name,out var at) || Environment.TickCount64>=at)
        {RefreshAt[name]=Environment.TickCount64+60000;Requests.Enqueue(name);}
        return texture;
    }
    internal static void Update()
    {
        if (Environment.TickCount64 < _requestAt || !Requests.TryDequeue(out var name)) return;
        _requestAt = Environment.TickCount64 + 650;
        Networking.Network.SendPacket(new GuildEmblemRequestPacket { GuildName = name });
    }
    internal static IGameTexture? Decode(GuildEmblemEntry entry) => entry.Image.Length == 0 ? null : Graphics.Renderer.CreateTextureFromStreamFactory("guild-emblem/" + entry.GuildId + "/" + entry.Revision, () => new MemoryStream(entry.Image, false));
    internal static void Apply(GuildEmblemStatePacket packet)
    {
        foreach (var entry in packet.Approved)
        {
            RefreshAt[entry.GuildName]=Environment.TickCount64+60000;
            Textures.TryGetValue(entry.GuildName, out var old);
            try { Textures[entry.GuildName] = Decode(entry); old?.Dispose(); }
            catch { Textures[entry.GuildName] = null; }
        }
    }
}

internal sealed class GuildEmblemWindow : Window
{
    private readonly TextBox _path;
    private readonly Label _status;
    private readonly ListBox _list;
    private readonly ImagePanel _preview;
    private readonly Button _submit, _approve, _reject, _remove, _previous, _next;
    private GuildEmblemEntry? _selected;
    private GuildEmblemStatePacket _state = new();
    private IGameTexture? _previewTexture;
    private static string T(string source) => Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source, ClientLanguages.Current);
    internal GuildEmblemWindow(Canvas parent) : base(parent, T("Guild emblem"), false, nameof(GuildEmblemWindow))
    {
        SetSize(700, 525); Alignment = [Alignments.Center]; DisableResizing(); DeleteOnClose = false;
        _status = new Label(this) { AutoSizeToContents = false, WrappingBehavior = WrappingBehavior.Wrapped, FontSize = 12, TextColorOverride = Color.White };
        _status.SetBounds(16, 12, 660, 68);
        _path = new TextBox(this) { Text = "" }; _path.SetBounds(16, 85, 490, 30);
        Action(T("Browse PNG"), 514, 85, () => Browse(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));
        _submit = Action(T("Submit for approval"), 16, 126, Submit, 250);
        Action(T("Refresh"), 282, 126, () => Request(GuildEmblemAction.Manage), 140);
        _list = new ListBox(this); _list.SetBounds(16, 174, 440, 216);
        _preview = new ImagePanel(this) { MaintainAspectRatio = true, MouseInputEnabled = false }; _preview.SetBounds(486, 192, 128, 128);
        _approve = Action(T("Approve"), 16, 405, () => Review(GuildEmblemAction.Approve), 140);
        _reject = Action(T("Reject"), 170, 405, () => Review(GuildEmblemAction.Reject), 140);
        _remove = Action(T("Remove emblem"), 324, 405, () => Review(GuildEmblemAction.Remove), 180);
        _previous = Action(T("Previous"), 16, 450, () => Request(GuildEmblemAction.Manage, _state.Page - 1), 140);
        _next = Action(T("Next"), 170, 450, () => Request(GuildEmblemAction.Manage, _state.Page + 1), 140);
        Hide();
    }
    protected override void EnsureInitialized() { }
    private Button Action(string text, int x, int y, System.Action callback, int width = 160)
    {
        var b = new Button(this) { Text = text }; b.SetBounds(x, y, width, 32); b.Clicked += (_, _) => callback(); return b;
    }
    private static void Request(GuildEmblemAction action, int page = 0) => Networking.Network.SendPacket(new GuildEmblemRequestPacket { Action = action, Page = Math.Max(0,page) });
    private void Submit()
    {
        try
        {
            var path = _path.Text.Trim().Trim('"');
            if (new FileInfo(path).Length > Intersect.Framework.Core.GuildEmblemPng.MaximumBytes) throw new InvalidDataException();
            var image = Intersect.Framework.Core.GuildEmblemPng.Sanitize(File.ReadAllBytes(path));
            Networking.Network.SendPacket(new GuildEmblemRequestPacket { Action = GuildEmblemAction.Submit, Image = image });
        }
        catch { _status.Text = T("Choose a valid RGB or RGBA PNG, at most 128 x 128 pixels and 64 KB."); }
    }
    private void Review(GuildEmblemAction action)
    {
        if (_selected == null) return;
        Networking.Network.SendPacket(new GuildEmblemRequestPacket { Action = action, GuildId = _selected.GuildId, Revision = _selected.Revision, Page = _state.Page });
    }
    internal void Apply(GuildEmblemStatePacket state)
    {
        _state = state; _selected = null; _list.RemoveAllRows();
        _preview.Texture = null; _previewTexture?.Dispose(); _previewTexture = null;
        _submit.IsDisabled = !state.CanSubmit;
        _status.Text = T(state.Message.Length > 0 ? state.Message : "Only Premium Game Master staff may submit an emblem. An administrator must approve it before publication.");
        _approve.IsDisabled = _reject.IsDisabled = _remove.IsDisabled = true;
        foreach (var approved in state.Approved)
        {
            var pending = state.Pending.FirstOrDefault(e => e.GuildId == approved.GuildId);
            var entry = pending ?? approved;
            var row = _list.AddRow(entry.GuildName + " - " + T(pending == null ? "Approved" : "Awaiting approval"));
            row.Clicked += (_, _) =>
            {
                _selected = entry; _preview.Texture = null; _previewTexture?.Dispose();
                _previewTexture = GuildEmblems.Decode(entry); _preview.Texture = _previewTexture;
                _approve.IsDisabled = _reject.IsDisabled = !state.CanModerate || pending == null;
                _remove.IsDisabled = !state.CanModerate || approved.Image.Length == 0;
            };
        }
        _previous.IsDisabled = state.Page <= 0; _next.IsDisabled = !state.HasMore;
        Show(); BringToFront();
    }
    private void Browse(string directory)
    {
        var browser = new WindowControl(Parent, T("Browse PNG"), false, "GuildEmblemFilePicker") { DeleteOnClose = true };
        browser.SetSize(620, 470); browser.Alignment = [Alignments.Center]; browser.DisableResizing();
        var path = new TextBox(browser) { Text = directory }; path.SetBounds(12, 12, 460, 30);
        var list = new ListBox(browser); list.SetBounds(12, 55, 584, 350);
        void Load(string folder)
        {
            try
            {
                folder = Path.GetFullPath(folder); path.Text = folder; list.RemoveAllRows();
                var parent = Directory.GetParent(folder);
                if (parent != null) list.AddRow("..").Clicked += (_, _) => Load(parent.FullName);
                foreach (var child in Directory.EnumerateDirectories(folder).OrderBy(p => p))
                    list.AddRow("[" + Path.GetFileName(child) + "]").Clicked += (_, _) => Load(child);
                foreach (var file in Directory.EnumerateFiles(folder, "*.png").OrderBy(p => p))
                    list.AddRow(Path.GetFileName(file)).Clicked += (_, _) => { _path.Text = file; browser.Close(); };
            }
            catch { _status.Text = T("This folder cannot be opened."); }
        }
        var open = new Button(browser) { Text = T("Open") }; open.SetBounds(480, 12, 116, 30); open.Clicked += (_, _) => Load(path.Text);
        Load(directory); browser.Show(); browser.BringToFront();
    }
}

public partial class GameInterface
{
    private GuildEmblemWindow? _guildEmblemWindow;
    public void ApplyGuildEmblems(GuildEmblemStatePacket packet)
    {
        GuildEmblems.Apply(packet);
        if (!packet.OpenWindow) return;
        _guildEmblemWindow ??= new GuildEmblemWindow(GameCanvas); _guildEmblemWindow.Apply(packet);
    }
}
