using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.WorldEvents;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Metalworking;
using Intersect.Network.Packets.Metalworking;
using R = Intersect.Client.Framework.GenericClasses.Rectangle;
namespace Intersect.Client.Interface.Game;

internal sealed class MetalworkWindow : Window
{
    private readonly ScrollControl _recipes; private readonly Label _name, _stage, _prompt; private readonly Workshop _scene; private readonly Meter _meter;
    private readonly Button _start, _pump, _strike, _collect; private readonly ImagePanel _output; private readonly List<Base> _materials = []; private readonly List<Button> _recipeButtons = [];
    private MetalworkStatePacket? _state; private long _received, _request, _soundAction = -1, _nextRefresh; private bool _closing; private readonly HashSet<Guid> _retired = [];
    private MetalworkingConfiguration? _catalog;
    internal MetalworkWindow(Canvas canvas) : base(canvas, ClientLanguages.Text("Royal Foundry & Forge"), false, nameof(MetalworkWindow))
    {
        SetSize(Math.Min(1000, canvas.Width - 20), Math.Min(720, canvas.Height - 35));
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();
        var side = Math.Min(220, Width / 4);
        var left = side + 30;
        var width = Width - left - 18;
        Text(this, "RecipeListHeading", 14, 12, side, 28).Text = ClientLanguages.Text("Metalworking recipes");
        _recipes = new ScrollControl(this, "MetalworkRecipes") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _recipes.SetBounds(12, 48, side, Height - 90);
        _name = Text(this, "MetalworkRecipeName", left, 12, width, 27);
        _name.FontSize = 14;
        _stage = Text(this, "MetalworkStage", left, 42, width, 27);
        _scene = new Workshop(this);
        _scene.SetBounds(left, 74, width, Height - 295);
        _output = new ImagePanel(_scene, "OutputItem") { MaintainAspectRatio = true, MouseInputEnabled = false };
        _output.SetBounds(width * 2 / 3 - 25, 24, 50, 50);
        _meter = new Meter(this);
        _meter.SetBounds(left, _scene.Bottom + 10, width, 26);
        _prompt = Text(this, "MetalworkInstructions", left, _meter.Bottom + 8, width, 50);
        _prompt.WrappingBehavior = WrappingBehavior.Wrapped;
        _start = ActionButton("Start recipe", left, Height - 65, 120, () => Send(MetalworkAction.Start));
        _pump = ActionButton("Pump bellows", left + 130, Height - 65, 140, () => Send(MetalworkAction.Pump));
        _strike = ActionButton("Strike", left + 130, Height - 65, 140, () => Send(MetalworkAction.Strike));
        _collect = ActionButton("Collect reward", left + 130, Height - 65, 140, () => Send(MetalworkAction.Collect));
        ActionButton("Leave", left + 280, Height - 65, Math.Max(80, width - 280), () => { _closing = true; Send(MetalworkAction.Leave); Hide(); });
        Closed += (_, _) => { _closing = true; Send(MetalworkAction.Leave); };
        Hide();
    }
    protected override void EnsureInitialized()
    {
    }
    private static Label Text(Base parent, string name, int x, int y, int w, int h)
    {
        var l = new Label(parent, name) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11, TextColorOverride = new Color(255, 244, 214, 160), AutoSizeToContents = false, MouseInputEnabled = false };
        l.SetBounds(x, y, w, h);
        return l;
    }
    private Button ActionButton(string text, int x, int y, int w, Action click)
    {
        var b = new Button(this, text.Replace(" ", "")) { Text = ClientLanguages.Text(text), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11 };
        b.SetBounds(x, y, w, 30);
        b.Clicked += (_, _) => click();
        return b;
    }
    private void Send(MetalworkAction action, Guid recipe = default)
    {
        if (_state == null)
            return;
        Intersect.Client.Networking.Network.SendPacket(new MetalworkRequestPacket { SessionId = _state.SessionId, RequestId = ++_request, Action = action, RecipeId = recipe });
    }
    internal void Apply(MetalworkStatePacket state)
    {
        if (_retired.Contains(state.SessionId))
            return;
        var changed = _state?.SessionId != state.SessionId;
        if (!changed && state.Sequence <= _state!.Sequence)
            return;
        if (changed)
        {
            if (_state != null)
                _retired.Add(_state.SessionId);
            _closing = false;
            _request = 0;
            _soundAction = -1;
            _nextRefresh = Environment.TickCount64 + 5000;
        }
        if (state.Closed)
        {
            _state = state;
            _closing = true;
            Hide();
            return;
        }
        if (_closing)
            return;
        var recipeChanged = changed || _state?.RecipeId != state.RecipeId;
        _state = state;
        _received = Environment.TickCount64;
        if (changed || !ReferenceEquals(_catalog, WorldMusic.Configuration.Metalworking))
        {
            _catalog = WorldMusic.Configuration.Metalworking;
            RefreshRecipes();
        }
        var recipe = _catalog?.Recipes.FirstOrDefault(r => r.Id == state.RecipeId);
        _name.Text = recipe == null ? ClientLanguages.Text("Choose a metalworking recipe.") : ClientLanguages.ContentText(recipe.Name);
        _output.Texture = GameContentManager.Current.GetTexture(TextureType.Item, ItemDescriptor.Get(recipe?.OutputItemId ?? Guid.Empty)?.Icon ?? "");
        _output.RenderColor = state.Paid ? Color.White : new Color(190, 255, 160, 100);
        if (recipeChanged)
            RefreshMaterials(recipe);
        _scene.State = state;
        _meter.State = state;
        _meter.Received = _received;
        if (state.ActionNumber != _soundAction)
        {
            _scene.PulseAt = _received;
            if (!string.IsNullOrWhiteSpace(state.Sound))
                Intersect.Client.Core.Audio.AddGameSound(state.Sound, false);
            _soundAction = state.ActionNumber;
        }
        _stage.Text = ClientLanguages.Text(!state.Running ? "Prepare your materials" : state.Stage switch
        {
            MetalworkStage.Heating => "Heat the metal",
            MetalworkStage.Pouring => "Pour into the moulds",
            MetalworkStage.Hammering => "Hammer the marked zone",
            MetalworkStage.Quenching => "Quench at the right temperature",
            _ => "Work completed"
        });
        if (state.Stage == MetalworkStage.Complete && state.Running)
            _stage.Text = ClientLanguages.Format("Quality: {0}%", state.Quality);
        _prompt.Text = state.Message.Length > 0 ? ClientLanguages.Text(state.Message) : ClientLanguages.Text(state.Stage == MetalworkStage.Heating ? "Keep the heat in the green zone with the bellows." : "Press when the white marker enters the green zone.");
        _start.IsDisabled = recipe == null || state.Running && state.Stage != MetalworkStage.Complete;
        _pump.IsHidden = state.Stage != MetalworkStage.Heating || !state.Running;
        _strike.IsHidden = state.Stage is MetalworkStage.Heating or MetalworkStage.Complete || !state.Running;
        _strike.Text = ClientLanguages.Text(state.Stage == MetalworkStage.Pouring ? "Pour" : state.Stage == MetalworkStage.Quenching ? "Quench" : "Hammer");
        _collect.IsHidden = !state.Running || state.Stage != MetalworkStage.Complete || !state.Successful || state.Paid;
        foreach (var button in _recipeButtons)
            button.IsDisabled = state.Running && state.Stage != MetalworkStage.Complete;
        if (changed)
        {
            Show();
            BringToFront();
        }
    }
    private void RefreshRecipes()
    {
        _recipes.DeleteAll();
        _recipeButtons.Clear();
        var y = 0;
        foreach (var recipe in (_catalog?.Recipes ?? []).Where(r => r.Enabled && r.Kind == _state?.Kind))
        {
            var button = new Button(_recipes, "Recipe" + recipe.Id) { Text = "" };
            button.SetBounds(0, y, _recipes.Width - 22, 83);
            _recipeButtons.Add(button);
            var icon = new ImagePanel(button, "Icon") { MaintainAspectRatio = true, MouseInputEnabled = false, Texture = GameContentManager.Current.GetTexture(TextureType.Item, ItemDescriptor.Get(recipe.OutputItemId)?.Icon ?? "") };
            icon.SetBounds(5, 7, 40, 40);
            var label = Text(button, "Name", 49, 7, Math.Max(55, button.Width - 54), 66);
            label.Text = ClientLanguages.ContentText(recipe.Name);
            label.WrappingBehavior = WrappingBehavior.Wrapped;
            button.SetToolTipText(ClientLanguages.ContentText(recipe.Name) + "\n" + string.Join(", ", recipe.Ingredients.Select(i => i.Quantity + " x " + ClientLanguages.ContentText(ItemDescriptor.GetName(i.ItemId)))));
            button.Clicked += (_, _) => Send(MetalworkAction.SelectRecipe, recipe.Id);
            y += 90;
        }
        if (y == 0)
            Text(_recipes, "NoRecipes", 5, 12, _recipes.Width - 20, 80).Text = ClientLanguages.Text("No metalworking recipes are available yet.");
        _recipes.SetInnerSize(_recipes.Width - 22, Math.Max(y, _recipes.Height - 4));
    }
    private void RefreshMaterials(MetalworkRecipe? recipe)
    {
        foreach (var control in _materials)
            RemoveChild(control, true);
        _materials.Clear();
        if (recipe == null)
            return;
        var x = _scene.X;
        foreach (var ingredient in recipe.Ingredients)
        {
            var item = ItemDescriptor.Get(ingredient.ItemId);
            var icon = new ImagePanel(this, "Material" + ingredient.ItemId) { Texture = GameContentManager.Current.GetTexture(TextureType.Item, item?.Icon ?? ""), MaintainAspectRatio = true };
            icon.SetBounds(x, Height - 119, 30, 30);
            icon.SetToolTipText(ClientLanguages.ContentText(item?.Name ?? "") + " x " + ingredient.Quantity);
            _materials.Add(icon);
            var label = Text(this, "Quantity" + ingredient.ItemId, x, Height - 88, 35, 16);
            label.Text = "x" + ingredient.Quantity;
            _materials.Add(label);
            x += 40;
        }
    }
    public override void Think()
    {
        if (IsHidden || _state == null || _closing)
            return;
        if (Environment.TickCount64 >= _nextRefresh)
        {
            _nextRefresh = Environment.TickCount64 + 5000;
            Send(MetalworkAction.Refresh);
        }
    }
    private sealed class Meter(Base parent) : Base(parent)
    {
        public MetalworkStatePacket? State; public long Received;
        protected override void RenderUnder(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var s = State;
            var r = skin.Renderer;
            r.DrawColor = new Color(255, 30, 20, 16);
            r.DrawFilledRect(RenderBounds);
            if (s == null)
                return;
            var target = s.Stage == MetalworkStage.Heating ? 67 : s.Target;
            var half = s.Stage == MetalworkStage.Heating ? 12 : 10;
            r.DrawColor = new Color(255, 55, 140, 70);
            r.DrawFilledRect(new R((int)(Width * (target - half) / 100d), 3, Math.Max(3, (int)(Width * half * 2 / 100d)), Height - 6));
            var value = s.Stage == MetalworkStage.Heating ? s.Heat : MetalworkRound.Cursor(s.Stage, s.StageElapsedMs + Environment.TickCount64 - Received);
            r.DrawColor = new Color(255, 255, 237, 165);
            r.DrawFilledRect(new R((int)(Math.Clamp(value, 0, 100) / 100 * Width) - 2, 0, 4, Height));
        }
    }
    private sealed class Workshop(Base parent) : Base(parent)
    {
        public MetalworkStatePacket? State; public long PulseAt;
        protected override void RenderUnder(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var renderer = skin.Renderer;
            var w = Width;
            var h = Height;
            var s = State;
            var t = Environment.TickCount64;
            void Fill(int x, int y, int width, int height, Color color)
            {
                renderer.DrawColor = color;
                renderer.DrawFilledRect(new R(x, y, Math.Max(1, width), Math.Max(1, height)));
            }
            Fill(0, 0, w, h, new Color(255, 25, 20, 19));
            Fill(0, h - 28, w, 28, new Color(255, 62, 43, 33));
            var fx = w / 7;
            var fy = h / 3;
            var fw = w / 3;
            var fh = h * 2 / 3 - 28;
            var heat = s?.Heat ?? 20;
            for (var row = 0; row < 5; row++)
            for (var col = 0; col < 5; col++)
                Fill(fx + col * fw / 5 + 1, fy + row * fh / 5 + 1, fw / 5 - 2, fh / 5 - 2, new Color(255, (byte)(70 + row * 6), 40, 30));
            Fill(fx + fw / 5, fy + fh / 3, fw * 3 / 5, fh * 2 / 3 - 8, new Color(255, 20, 12, 10));
            for (var i = 0; i < 8; i++)
            {
                var flicker = (Math.Sin(t / 130d + i * 2) + 1) / 2;
                var flame = (int)(fh * (.15 + flicker * .23) * heat / 100);
                Fill(fx + fw / 5 + i * fw * 3 / 40, fy + fh - 12 - flame, Math.Max(3, fw / 16), flame, new Color(200, (byte)255, (byte)(70 + flicker * 110), 25));
            }
            var ax = w * 2 / 3;
            var ay = h * 2 / 3;
            if (s?.Kind == MetalworkKind.Smelting)
            {
                for (var i = 0; i < 3; i++)
                {
                    Fill(ax - 45 + i * 36, ay + 10, 31, 20, new Color(255, 28, 26, 24));
                    if ((s?.Hits ?? 0) > i || s?.Paid == true)
                        Fill(ax - 42 + i * 36, ay + 13, 25, 11, new Color(255, 245, 153, 45));
                }
                if (t - PulseAt < 500 && s?.Stage == MetalworkStage.Pouring)
                    Fill(fx + fw - 6, fy + fh / 2, ax - fx - fw + 12, 8, new Color(230, 255, 170, 50));
            }
            else
            {
                Fill(ax - 58, ay, 116, 18, new Color(255, 105, 110, 121));
                Fill(ax - 25, ay + 18, 50, 38, new Color(255, 64, 68, 80));
                Fill(ax - 45, ay + 55, 90, 12, new Color(255, 92, 96, 110));
                Fill(ax - 36, ay - 8, 72, 9, new Color(255, 255, (byte)(90 + heat), 45));
                var hit = t - PulseAt < 300 ? 18 : 0;
                Fill(ax + 22, ay - 92 + hit, 10, 62, new Color(255, 138, 86, 41));
                Fill(ax + 3, ay - 98 + hit, 48, 22, new Color(255, 175, 179, 188));
                if (s?.Stage == MetalworkStage.Quenching)
                {
                    Fill(w - 70, h - 84, 50, 56, new Color(255, 47, 89, 122));
                    Fill(w - 68, h - 80, 46, 8, new Color(255, 110, 185, 215));
                }
            }
            if (t - PulseAt < 550)
            for (var i = 0; i < 14; i++)
            {
                var age = (t - PulseAt) / 550d;
                var px = ax + (int)(Math.Cos(i * 2.4) * age * 90);
                var py = ay - (int)(Math.Sin(i * 2.4) * age * 70 + age * 50);
                Fill(px, py, 3, 3, new Color((byte)(255 * (1 - age)), 255, 204, 90));
            }
        }
    }
}
