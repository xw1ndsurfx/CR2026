using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.GenericClasses;
namespace Intersect.Client.Interface.Game;
public partial class MenuContainer
{
    // Small monochrome pixel glyphs keep the menu usable with incomplete GUI installations.
    // Existing square icons remain preferred; framed Btn_* artwork is not squeezed into a row.
    private sealed class MenuGlyph : Base
    {
        private readonly string[] _pixels;
        internal MenuGlyph(Base parent, string caption) : base(parent, "MenuGlyph" + caption.Replace(" ", ""))
        {
            MouseInputEnabled = false; KeyboardInputEnabled = false;
            var pattern = caption switch
            {
                "Dungeons" => "#.#..#.#/#.####.#/########/.######./.##..##./.##..##./.##..##./.##..##.",
                "Daily Reward" => ".#....#./########/#......#/########/#.##.#.#/#.#.##.#/#.##.#.#/########",
                "LogiCoin Shop" => "..####../.######./########/.#.##.#./..####../...##.../...##.../........",
                "News" => "#######./#.....#./#.###.#./#.....#./#.###.#./#.....##/#.###.##/########",
                "Inventory" => "..####../...##.../..####../.######./##.##.##/##....##/.######./..####..",
                "Spell Book" => "#######./#..#..#./#.###.#./#..#..#./#.###.#./#..#..#./#######./........",
                "Character" => "..####../..####../...##.../.######./##.##.##/...##.../..#..#../.##..##.",
                "Quest Log" => "..##..../..##..../..##..../..##..../..##..../......../..##..../..##....",
                "Friends" or "Party" => ".##..##./.##..##./......../###..###/########/########/###..###/###..###",
                "My house" => "...##.../..####../.######./########/.######./.##..##./.##..##./.######.",
                "Guild" => "########/##....##/.##..##./..####../...##.../..####../...##.../..####..",
                _ => "......../########/########/......../########/########/......../........",
            };
            _pixels = pattern.Split('/');
        }
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var x = (Width - 16) / 2; var y = (Height - 16) / 2;
            for (var row = 0; row < _pixels.Length; row++)
                for (var col = 0; col < _pixels[row].Length; col++)
                    if (_pixels[row][col] == '#') skin.DrawRectFill(new Rectangle(x + col * 2, y + row * 2, 2, 2), Color.White);
        }
    }
}
