namespace Intersect.Client.Entities;

internal readonly record struct NameplateLayout(float Footer, float Name, float Header, float Afk, float Guild, float ChatBubble)
{
    internal static NameplateLayout Create(float graphicTop, int hpBarHeight, float nameHeight,
        float footerHeight, float headerHeight, float afkHeight, float guildHeight)
    {
        var bottom = graphicTop - 4 - (hpBarHeight > 0 ? hpBarHeight + 2 : 0);
        var footer = bottom - footerHeight;
        var name = footer - (footerHeight > 0 ? 2 : 0) - nameHeight;
        var header = name - (headerHeight > 0 ? headerHeight + 2 : 0);
        var afk = header - (afkHeight > 0 ? afkHeight + 2 : 0);
        var guild = afk - (guildHeight > 0 ? guildHeight + 2 : 0);
        return new(footer, name, header, afk, guild, guild - 6);
    }
}
