using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Entities;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.GameObjects.Events;

namespace Intersect.Client.Entities;

public partial class Entity
{
    private void DrawFishingEquipment(bool front, Color renderColor)
    {
        if (Maps.MapInstance.Get(MapId)?.HideEquipment == true || !string.IsNullOrEmpty(TransformedSprite) ||
            !FishingWorldPresentation.TryGet(this, out var presentation, out var clip, out var frame)) return;
        var profile = presentation.Profile;
        var file = front ? clip.Front : clip.Back;
        if (string.IsNullOrWhiteSpace(file) || GameContentManager.Current.GetTexture(TextureType.Gui, file) is not { } texture) return;
        var row = profile.DirectionRows[Math.Clamp(PickSpriteRow(DirectionFacing), 0, 3)];
        var source = new FloatRect(frame * profile.FrameWidth, row * profile.FrameHeight, profile.FrameWidth, profile.FrameHeight);
        if (source.Right > texture.Width || source.Bottom > texture.Height) return;
        var width = profile.FrameWidth * (float)profile.Scale;
        var height = profile.FrameHeight * (float)profile.Scale;
        var position = EventGraphicPlacement.TopLeft(Origin.X - TileWidth / 2f, Origin.Y - TileHeight,
            width, height, TileWidth, TileHeight) + _spriteAnchorOffset;
        var destination = new FloatRect(position.X + profile.OffsetX, position.Y + profile.OffsetY, width, height);
        DrawRiderLayer(texture, source, destination, renderColor);
    }

    private bool IsFishingEquipment(Guid itemId) => FishingWorldPresentation.TryGet(this, out var state, out var clip, out _) && state.ToolItemId == itemId &&
        (clip.Front.Length > 0 && GameContentManager.Current.GetTexture(TextureType.Gui, clip.Front) != null ||
         clip.Back.Length > 0 && GameContentManager.Current.GetTexture(TextureType.Gui, clip.Back) != null);

    private void ApplyFishingBodyAnimation()
    {
        if (IsMoving || IsAttacking || IsCasting || !FishingWorldPresentation.TryGet(this, out _, out var clip, out var frame) ||
            !Enum.TryParse<SpriteAnimations>(clip.BodyAnimation, out var body) || body == SpriteAnimations.Normal || !AnimatedTextures.ContainsKey(body)) return;
        SpriteAnimation = body;
        SpriteFrame = Math.Clamp((int)(frame / (double)Math.Max(1, clip.Frames) * SpriteFrames), 0, Math.Max(0, SpriteFrames - 1));
    }
}
