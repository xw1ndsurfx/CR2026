using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.QualityOfLife;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game;

internal sealed partial class AchievementsWindow
{
    private static void ApplyStateIcon(ImagePanel icon, Base card, AchievementDefinition definition, bool completed)
    {
        var requested = completed || string.IsNullOrWhiteSpace(definition.LockedIcon) ? definition.Icon : definition.LockedIcon;
        var texture = string.IsNullOrWhiteSpace(requested) ? null : Globals.ContentManager?.GetTexture(TextureType.Achievement, requested);
        var fallback = !completed && (string.IsNullOrWhiteSpace(definition.LockedIcon) || texture == null);
        if (fallback && !string.IsNullOrWhiteSpace(definition.Icon))
            texture = Globals.ContentManager?.GetTexture(TextureType.Achievement, definition.Icon);
        icon.Texture = texture;
        icon.RenderColor = fallback ? new Color(145, 145, 145) : Color.White;
        icon.IsHidden = texture == null;
        icon.MouseInputEnabled = true;
        icon.SetToolTipText(QualityOfLifeText.Get(completed ? "Unlocked" : "Locked", ClientLanguages.Current));
        if (fallback)
        {
            var badge = new AchievementLockBadge(card, "AchievementLock" + definition.Id) { MouseInputEnabled = false, KeyboardInputEnabled = false };
            badge.SetBounds(73, 73, 24, 24);
        }
    }

    private sealed class AchievementLockBadge(Base parent, string name) : Base(parent, name)
    {
        protected override void Render(SkinBase skin)
        {
            skin.Renderer.DrawColor = new Color(28, 18, 17);
            skin.Renderer.DrawFilledRect(new UiRectangle(0, 0, 24, 24));
            skin.Renderer.DrawColor = Muted;
            skin.Renderer.DrawFilledRect(new UiRectangle(7, 3, 10, 3));
            skin.Renderer.DrawFilledRect(new UiRectangle(5, 5, 3, 8));
            skin.Renderer.DrawFilledRect(new UiRectangle(16, 5, 3, 8));
            skin.Renderer.DrawFilledRect(new UiRectangle(4, 11, 16, 11));
            skin.Renderer.DrawColor = new Color(28, 18, 17);
            skin.Renderer.DrawFilledRect(new UiRectangle(11, 14, 3, 5));
        }
    }
}
