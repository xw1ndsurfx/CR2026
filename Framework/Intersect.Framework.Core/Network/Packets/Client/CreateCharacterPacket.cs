using Intersect.Collections;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.GameObjects;
using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class CreateCharacterPacket : IntersectPacket
{
    //Parameterless Constructor for MessagePack
    public CreateCharacterPacket()
    {
    }

    public CreateCharacterPacket(string name, Guid classId, int sprite, CharacterAppearance? appearance = null)
    {
        Name = name;
        ClassId = classId;
        Sprite = sprite;
        Appearance = appearance?.SanitizedCopy() ?? new CharacterAppearance();
    }

    [Key(0)]
    public string Name { get; set; }

    [Key(1)]
    public Guid ClassId { get; set; }

    [Key(2)]
    public int Sprite { get; set; }

    [Key(3)]
    public CharacterAppearance Appearance { get; set; } = new();

    public override Dictionary<string, SanitizedValue<object>> Sanitize()
    {
        base.Sanitize();

        var sanitizer = new Sanitizer();

        var classDescriptor = ClassDescriptor.Get(ClassId);
        if (classDescriptor != null)
        {
            var maximumSprite = Math.Max(0, (classDescriptor.Sprites?.Count ?? 1) - 1);
            Sprite = sanitizer.Clamp(nameof(Sprite), Sprite, 0, maximumSprite);
        }

        Appearance = (Appearance ?? new CharacterAppearance()).SanitizedCopy();

        return sanitizer.Sanitized;
    }

}
