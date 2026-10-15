namespace Intersect.Client.Interface.Game;

internal sealed partial class CookingWindow
{
    private void DrawKitchenEnvironment(
        Action<int, int, int, int, Color> fill,
        long now
    )
    {
        // Warm stone/plaster back wall.
        fill(0, 0, 1024, 720, new Color(a: 255, r: 42, g: 29, b: 21));
        fill(0, 74, 1024, 318, new Color(a: 255, r: 78, g: 64, b: 50));

        // Hand-built tile/brick backsplash.
        for (var row = 0; row < 10; ++row)
        {
            var brickY = 80 + row * 31;
            var offset = row % 2 == 0 ? 0 : 23;
            for (var column = -1; column < 24; ++column)
            {
                var brickX = column * 48 + offset;
                var shade = 86 + ((row + column + 12) % 3) * 7;
                fill(
                    brickX,
                    brickY,
                    45,
                    28,
                    new Color(a: 255, r: shade, g: shade - 18, b: shade - 31)
                );
                fill(brickX, brickY + 27, 45, 1, new Color(a: 175, r: 44, g: 35, b: 29));
            }
        }

        // Left stone hearth, deliberately behind the recipe board so it reads as room depth.
        fill(0, 150, 224, 290, new Color(a: 255, r: 65, g: 45, b: 34));
        fill(18, 184, 186, 226, new Color(a: 255, r: 36, g: 24, b: 19));
        fill(28, 200, 166, 200, new Color(a: 255, r: 25, g: 18, b: 15));

        // Animated oven fire.
        for (var flame = 0; flame < 9; ++flame)
        {
            var wave = (int)((now / 85 + flame * 17) % 22);
            var height = 22 + (wave % 14);
            var x = 42 + flame * 16;
            fill(x, 371 - height, 12, height, new Color(a: 220, r: 232, g: 110, b: 39));
            fill(x + 3, 371 - height + 8, 7, Math.Max(5, height - 12), new Color(a: 230, r: 251, g: 182, b: 65));
        }

        // Back counter and lower cabinets.
        fill(0, 390, 1024, 330, new Color(a: 255, r: 49, g: 29, b: 19));
        fill(0, 390, 1024, 20, new Color(a: 255, r: 133, g: 79, b: 40));
        fill(0, 410, 1024, 25, new Color(a: 255, r: 91, g: 52, b: 29));

        for (var cabinet = 0; cabinet < 7; ++cabinet)
        {
            var x = cabinet * 146;
            fill(x + 4, 455, 136, 248, new Color(a: 255, r: 73, g: 41, b: 25));
            fill(x + 10, 466, 124, 225, new Color(a: 255, r: 91, g: 53, b: 30));
            fill(x + 18, 477, 108, 203, new Color(a: 255, r: 61, g: 35, b: 23));
            fill(x + 67, 470, 7, 7, new Color(a: 255, r: 194, g: 133, b: 58));
        }

        // Large center preparation island.
        fill(376, 354, 604, 151, new Color(a: 255, r: 66, g: 38, b: 23));
        fill(386, 362, 584, 133, new Color(a: 255, r: 125, g: 78, b: 42));
        fill(386, 362, 584, 10, new Color(a: 255, r: 197, g: 141, b: 74));
        fill(398, 475, 560, 12, new Color(a: 255, r: 72, g: 43, b: 27));

        // Cutting board.
        fill(446, 390, 222, 76, new Color(a: 255, r: 85, g: 51, b: 29));
        fill(452, 396, 210, 64, new Color(a: 255, r: 162, g: 106, b: 57));
        fill(458, 402, 198, 4, new Color(a: 160, r: 232, g: 178, b: 103));

        // Stove embedded in the island.
        fill(700, 378, 230, 90, new Color(a: 255, r: 54, g: 49, b: 44));
        fill(710, 387, 210, 70, new Color(a: 255, r: 36, g: 32, b: 29));

        for (var burner = 0; burner < 3; ++burner)
        {
            var bx = 728 + burner * 60;
            fill(bx, 404, 42, 42, new Color(a: 255, r: 21, g: 20, b: 19));
            fill(bx + 7, 411, 28, 28, new Color(a: 255, r: 76, g: 61, b: 48));
            fill(bx + 12, 416, 18, 18, new Color(a: 255, r: 29, g: 25, b: 22));
        }

        // Pantry shelves on the right wall.
        fill(838, 104, 160, 220, new Color(a: 255, r: 63, g: 38, b: 24));
        for (var shelf = 0; shelf < 3; ++shelf)
        {
            var sy = 141 + shelf * 63;
            fill(848, sy, 140, 8, new Color(a: 255, r: 136, g: 83, b: 41));

            for (var jar = 0; jar < 5; ++jar)
            {
                var jx = 854 + jar * 27;
                var body = (jar % 3) switch
                {
                    0 => new Color(a: 255, r: 126, g: 78, b: 42),
                    1 => new Color(a: 255, r: 94, g: 108, b: 57),
                    _ => new Color(a: 255, r: 157, g: 118, b: 64),
                };
                fill(jx, sy - 27, 18, 23, body);
                fill(jx + 3, sy - 31, 12, 5, new Color(a: 255, r: 207, g: 178, b: 122));
            }
        }

        // Hanging copper cookware.
        fill(594, 80, 226, 7, new Color(a: 255, r: 91, g: 56, b: 33));
        for (var pan = 0; pan < 5; ++pan)
        {
            var px = 612 + pan * 41;
            var length = 42 + (pan % 3) * 12;
            fill(px + 12, 86, 4, length, new Color(a: 255, r: 102, g: 66, b: 40));
            fill(px, 86 + length, 28, 24, new Color(a: 255, r: 155, g: 83, b: 42));
            fill(px + 5, 91 + length, 18, 14, new Color(a: 255, r: 196, g: 108, b: 50));
        }

        // Small kitchen window with cool outside light to contrast the warm room.
        fill(934, 114, 80, 186, new Color(a: 255, r: 47, g: 34, b: 27));
        fill(942, 123, 64, 166, new Color(a: 255, r: 64, g: 97, b: 107));
        fill(972, 123, 5, 166, new Color(a: 255, r: 58, g: 39, b: 28));
        fill(942, 202, 64, 5, new Color(a: 255, r: 58, g: 39, b: 28));

        // Lanterns and warm light pools.
        for (var lantern = 0; lantern < 2; ++lantern)
        {
            var lx = lantern == 0 ? 112 : 900;
            fill(lx, 12, 34, 42, new Color(a: 255, r: 55, g: 34, b: 23));
            fill(lx + 6, 18, 22, 28, new Color(a: 255, r: 239, g: 163, b: 67));
            fill(lx + 10, 22, 14, 20, new Color(a: 235, r: 255, g: 211, b: 98));
        }

        // Pots / ingredients on the work surface.
        fill(770, 324, 132, 55, new Color(a: 255, r: 79, g: 50, b: 34));
        fill(782, 310, 108, 21, new Color(a: 255, r: 101, g: 61, b: 39));

        for (var item = 0; item < 8; ++item)
        {
            var ix = 402 + item * 32;
            var iy = 345 - (item % 3) * 7;
            var ingredient = (item % 4) switch
            {
                0 => new Color(a: 255, r: 210, g: 95, b: 38),
                1 => new Color(a: 255, r: 99, g: 132, b: 57),
                2 => new Color(a: 255, r: 222, g: 204, b: 151),
                _ => new Color(a: 255, r: 150, g: 54, b: 36),
            };
            fill(ix, iy, 18, 15, ingredient);
            fill(ix + 6, iy - 5, 5, 7, new Color(a: 255, r: 73, g: 114, b: 54));
        }

        // Steam above the main pot.
        for (var puff = 0; puff < 5; ++puff)
        {
            var rise = (int)((now / 28 + puff * 23) % 72);
            var px = 806 + puff * 12 + ((puff % 2 == 0 ? 1 : -1) * rise / 14);
            fill(
                px,
                304 - rise,
                12 + puff % 2 * 5,
                10 + puff % 3 * 5,
                new Color(a: 58, r: 226, g: 222, b: 211)
            );
        }

        // Foreground shadow/vignette anchors the kitchen in the room.
        fill(0, 704, 1024, 16, new Color(a: 190, r: 10, g: 7, b: 5));
        fill(0, 0, 18, 720, new Color(a: 140, r: 8, g: 6, b: 5));
        fill(1006, 0, 18, 720, new Color(a: 140, r: 8, g: 6, b: 5));
    }
}
