using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using Color = Intersect.Color;

// Software QA of the real control tree; native-style surfaces, no GPU/font-atlas emulation.
internal sealed class EquipmentSkin : Intersect.Client.Framework.Gwen.Skin.Base
{
    internal EquipmentSkin(DrawingRenderer renderer) : base(renderer)
    {
        DefaultFont = new TestFont(); DefaultFontSize = 12;
        Colors.Label.Normal = Colors.Label.Disabled = Color.White;
        Colors.Button.Normal = Colors.Button.Hover = Colors.Button.Active = new Color(255, 245, 224, 180);
        Colors.Button.Disabled = new Color(255, 135, 128, 115);
        Colors.Window.TitleActive = Colors.Window.TitleInactive = Color.White;
    }
    public override void DrawButton(Button button)
    {
        Renderer.DrawColor = new Color(255, 160, 121, 65); Renderer.DrawFilledRect(new Rectangle(0, 0, button.Width, button.Height));
        Renderer.DrawColor = button.IsDisabled ? new Color(255, 38, 35, 30) : new Color(255, 68, 43, 28); Renderer.DrawFilledRect(new Rectangle(2, 2, button.Width - 4, button.Height - 4));
    }
    public override void DrawTextBox(Base control)
    {
        Renderer.DrawColor = new Color(255, 145, 114, 70); Renderer.DrawFilledRect(new Rectangle(0, 0, control.Width, control.Height));
        Renderer.DrawColor = new Color(255, 22, 18, 15); Renderer.DrawFilledRect(new Rectangle(1, 1, control.Width - 2, control.Height - 2));
    }
}
