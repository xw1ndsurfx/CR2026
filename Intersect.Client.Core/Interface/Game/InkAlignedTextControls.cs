using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal interface IInkAlignedText { int InkOffsetY { get; set; } }

internal readonly record struct InkLayoutStamp(int Width, int Height, int TextWidth, int TextHeight,
    int X, int Y, int Left, int Top, int Right, int Bottom, Pos Alignment, int Offset)
{
    internal static InkLayoutStamp For(Label label, Text text, int offset) => new(label.Width, label.Height,
        text.OuterWidth, text.OuterHeight, text.X, text.Y, label.Padding.Left, label.Padding.Top,
        label.Padding.Right, label.Padding.Bottom, label.TextAlign, offset);
}

internal sealed class InkAlignedLabel(Base parent, string name) : Label(parent, name), IInkAlignedText
{
    public int InkOffsetY { get; set; }
    private InkLayoutStamp _last;
    protected override void Render(SkinBase skin)
    {
        if (InkLayoutStamp.For(this, _textElement, InkOffsetY) != _last)
        {
            AlignTextElement(_textElement);
            _textElement.SetPosition(_textElement.X, _textElement.Y + InkOffsetY);
            _last = InkLayoutStamp.For(this, _textElement, InkOffsetY);
        }
        base.Render(skin);
    }
}

internal sealed class InkAlignedButton(Base parent, string name) : Button(parent, name), IInkAlignedText
{
    public int InkOffsetY { get; set; }
    private InkLayoutStamp _last;
    protected override void Render(SkinBase skin)
    {
        if (InkLayoutStamp.For(this, _textElement, InkOffsetY) != _last)
        {
            AlignTextElement(_textElement);
            _textElement.SetPosition(_textElement.X, _textElement.Y + InkOffsetY);
            _last = InkLayoutStamp.For(this, _textElement, InkOffsetY);
        }
        base.Render(skin);
    }
}
