using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Interface.Game.DescriptionWindows.Components;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.File_Management;

namespace Intersect.Client.Interface.Game.DescriptionWindows;

internal sealed class MobileDescriptionActionBar : ComponentBase
{
    private readonly Button _action;
    private readonly Button _close;
    private bool _finished;

    internal MobileDescriptionActionBar(Base parent, string? caption, Func<bool> canExecute,
        Action execute, Action close) : base(parent, "MobileDescriptionActions")
    {
        MouseInputEnabled = true;
        SetSize(280, 52);
        var font = GameContentManager.Current.GetFont("sourcesansproblack") ??
            GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont;
        _action = new ReadableActionButton(this, "MobilePrimaryAction", caption ?? string.Empty, font);
        _close = new ReadableActionButton(this, "MobileCloseDescription", ClientLanguages.Text("Close"), font);
        _action.IsVisibleInParent = caption != null;
        _action.Clicked += (_, _) =>
        {
            if (_finished) return;
            _finished = true;
            var allowed = canExecute();
            close();
            if (allowed) execute();
        };
        _close.Clicked += (_, _) => { if (!_finished) { _finished = true; close(); } };
        CorrectWidth();
    }

    public override void CorrectWidth()
    {
        var minimumAction = Skin.Renderer.MeasureText(_action.Font, _action.FontSize, _action.Text).X + 24;
        var minimumClose = Math.Max(96, Skin.Renderer.MeasureText(_close.Font, _close.FontSize, _close.Text).X + 24);
        SetSize(Math.Max(Math.Max(280, minimumAction + minimumClose + 16), Parent?.InnerWidth ?? 280), 52);
        var closeWidth = _action.IsVisibleInParent ? minimumClose : Width - 8;
        _close.SetBounds(Width - closeWidth - 4, 4, closeWidth, 44);
        _action.SetBounds(4, 4, Width - closeWidth - 12, 44);
    }

    private sealed class ReadableActionButton : Button
    {
        internal ReadableActionButton(Base parent, string name, string text, IFont font) : base(parent, name)
        {
            Text = text;
            Font = font;
            FontSize = 14;
            TextColorOverride = Color.White;
            ShouldDrawBackground = false;
        }

        protected override void Render(Framework.Gwen.Skin.Base skin)
        {
            var renderer = skin.Renderer;
            var oldColor = renderer.DrawColor;
            try
            {
                renderer.DrawColor = new Color(255, 24, 35, 49);
                renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height));
                renderer.DrawColor = new Color(255, 188, 151, 76);
                renderer.DrawFilledRect(new Rectangle(0, 0, Width, 2));
                renderer.DrawFilledRect(new Rectangle(0, Height - 2, Width, 2));
                renderer.DrawFilledRect(new Rectangle(0, 0, 2, Height));
                renderer.DrawFilledRect(new Rectangle(Width - 2, 0, 2, Height));
            }
            finally { renderer.DrawColor = oldColor; }
            base.Render(skin);
        }
    }
}
