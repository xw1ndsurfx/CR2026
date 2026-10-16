using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
namespace Intersect.Client.Interface.Game;
internal static class EventPanelText
{
    internal static void Fit(Label label, string text, int maximum = 12, int minimum = 9)
    {
        label.TooltipText = text;
        label.FontSize = maximum;
        while (label.FontSize > minimum && label.Skin.Renderer.MeasureText(label.Font, label.FontSize, text).X > label.Width - 6) label.FontSize--;
        var visible = text;
        if (label.Skin.Renderer.MeasureText(label.Font, label.FontSize, visible).X > label.Width - 6)
        {
            var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(text);
            var low = 0; var high = boundaries.Length;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                var end = middle == boundaries.Length ? text.Length : boundaries[middle];
                if (label.Skin.Renderer.MeasureText(label.Font, label.FontSize, text[..end] + "...").X <= label.Width - 6) low = middle;
                else high = middle - 1;
            }
            visible = text[..(low == boundaries.Length ? text.Length : boundaries[low])] + "...";
        }
        label.Text = visible;
    }
    internal static RichLabel CreateBody(ScrollControl scroll, string name, int size = 11)
    {
        var text = new RichLabel(scroll, name)
        {
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? scroll.Skin.DefaultFont,
            FontSize = size, Padding = new Padding(4, 4, 8, 8), ShouldDrawBackground = false
        };
        QuestTextScroller.Configure(scroll, text);
        return text;
    }
    internal static void SetBody(ScrollControl scroll, RichLabel text, string content)
    {
        text.SetSize(Math.Max(1, scroll.Width - 22), 20);
        text.ClearText(); text.AddText(content, Color.White); text.ForceImmediateRebuild();
        scroll.UpdateScrollBars();
    }
}
