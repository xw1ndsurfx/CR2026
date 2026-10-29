using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Interface.Game;

public partial class MenuContainer
{
    private sealed class MenuGlyph : Base
    {
        private readonly string _caption;
        private static readonly string[] HousePixels =
            ["...##...", "..####..", ".######.", "########", ".######.", ".##..##.", ".##..##.", ".######."];

        internal MenuGlyph(Base parent, string caption) : base(parent, "MenuGlyph" + caption.Replace(" ", ""))
        {
            _caption = caption;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            if (CompactMenuArtwork.TryGetValue(_caption, out var artwork))
            {
                var color = Parent is MenuRow row ? row.IconColor : Color.White;
                DrawArtwork(skin, artwork.Width, artwork.Rows, color);
                if (artwork.AccentRows is { } accents)
                    DrawArtwork(skin, artwork.Width, accents, new Color(255, 152, 107, 86));
                return;
            }

            // Preserve the existing house glyph: no house artwork was supplied.
            var houseX = (Width - 16) / 2;
            var houseY = (Height - 16) / 2;
            for (var y = 0; y < HousePixels.Length; y++)
                for (var x = 0; x < HousePixels[y].Length; x++)
                    if (HousePixels[y][x] == '#')
                        skin.DrawRectFill(new Rectangle(houseX + x * 2, houseY + y * 2, 2, 2), Color.White);
        }
        private void DrawArtwork(Intersect.Client.Framework.Gwen.Skin.Base skin, int sourceWidth, ulong[] rows, Color color)
        {
            var scale = Math.Min(Width / (float)sourceWidth, Height / (float)rows.Length);
            var width = (int)MathF.Round(sourceWidth * scale);
            var height = (int)MathF.Round(rows.Length * scale);
            if (width <= 0 || height <= 0) return;
            var left = (Width - width) / 2;
            var top = (Height - height) / 2;
            // Nearest-neighbor sampling keeps the supplied pixel art sharp.
            for (var y = 0; y < height; y++)
            {
                var pixels = rows[y * rows.Length / height];
                for (var x = 0; x < width;)
                {
                    if ((pixels & (1UL << (x * sourceWidth / width))) == 0) { x++; continue; }
                    var start = x++;
                    while (x < width && (pixels & (1UL << (x * sourceWidth / width))) != 0) x++;
                    skin.DrawRectFill(new Rectangle(left + start, top + y, x - start, 1), color);
                }
            }
        }
    }
}
