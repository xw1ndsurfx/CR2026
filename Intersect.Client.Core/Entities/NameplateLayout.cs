namespace Intersect.Client.Entities;

internal readonly record struct NameplateLayout(float Footer, float Name, float Header, float Afk, float Guild, float ChatBubble)
{
    internal static NameplateLayout Create(float graphicTop, int hpBarHeight, float nameHeight,
        float footerHeight, float headerHeight, float afkHeight, float guildHeight, float spacingScale = 1f)
    {
        var gap = 2 * spacingScale;
        var bottom = graphicTop - 4 * spacingScale - (hpBarHeight > 0 ? hpBarHeight + gap : 0);
        var footer = bottom - footerHeight;
        var name = footer - (footerHeight > 0 ? gap : 0) - nameHeight;
        var header = name - (headerHeight > 0 ? headerHeight + gap : 0);
        var afk = header - (afkHeight > 0 ? afkHeight + gap : 0);
        var guild = afk - (guildHeight > 0 ? guildHeight + gap : 0);
        return new(footer, name, header, afk, guild, guild - 6 * spacingScale);
    }
}
