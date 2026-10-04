using Intersect.Framework.Core;
using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class ApplyRoyalStylistAppearancePacket : IntersectPacket
{
    public ApplyRoyalStylistAppearancePacket()
    {
    }

    public ApplyRoyalStylistAppearancePacket(Guid stylistId, CharacterAppearance appearance)
    {
        StylistId = stylistId;
        Appearance = appearance ?? new CharacterAppearance();
    }

    [Key(0)]
    public Guid StylistId { get; set; }

    [Key(1)]
    public CharacterAppearance Appearance { get; set; } = new();
}
