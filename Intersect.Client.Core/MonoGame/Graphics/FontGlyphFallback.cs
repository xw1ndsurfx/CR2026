using System.Text;

namespace Intersect.Client.MonoGame.Graphics;

/// <summary>Display-only equivalents for typography missing from the selected font.</summary>
internal sealed class FontGlyphFallback(IEnumerable<char> characters, char defaultCharacter)
{
    private readonly HashSet<char> _characters = characters.ToHashSet();

    internal string Prepare(string text)
    {
        StringBuilder? output = null;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            // SpriteFont handles line breaks itself; they are not atlas glyphs.
            if (_characters.Contains(character) || character is '\r' or '\n')
            {
                output?.Append(character);
                continue;
            }

            var replacement = character switch
            {
                '\u00D7' => "x",
                '\u00F7' => "/",
                '\u2039' => "<",
                '\u203A' => ">",
                '\u00AB' => "<<",
                '\u00BB' => ">>",
                '\u2026' => "...",
                '\u2018' or '\u2019' or '\u201A' or '\u2032' => "'",
                '\u201C' or '\u201D' or '\u201E' or '\u2033' => "\"",
                '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212' => "-",
                '\u00B7' => ".",
                '\u2022' => "*",
                '\u00A0' or '\u2007' or '\u202F' => " ",
                '\t' => "    ",
                '\u2190' => "<-",
                '\u2192' => "->",
                _ => null,
            };

            output ??= new StringBuilder(text.Length + 8).Append(text, 0, index);
            if (replacement != null && replacement.All(_characters.Contains))
                output.Append(replacement);
            else
                output.Append(defaultCharacter);
        }
        return output?.ToString() ?? text;
    }
}
