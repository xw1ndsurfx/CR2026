using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Networking;

namespace Intersect.Client.Interface.Game.Character;

public partial class CharacterWindow
{
    private void BuildCharacterActions()
    {
        // Keep the legacy character.png at its authored dimensions. Stretching
        // the whole window moves its painted stat/equipment backgrounds away
        // from the JSON controls. Seven actions fit in three compact rows.
        const int margin = 12, gap = 4, height = 22;
        var width = Math.Max(60, (mCharacterWindow.Width - 2 * margin - 2 * gap) / 3);
        var statsBottom = new[] { mAttackLabel, mDefenseLabel, mSpeedLabel, mAbilityPwrLabel, mMagicRstLabel }
            .Max(label => label.Bottom);
        var footerHeight = 3 * height + 2 * gap;
        var preferredTop = _detailsButton.Y;
        var actions = new ScrollControl(mCharacterWindow, "CharacterActions")
        {
            OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true,
        };
        _detailsButton.Parent = actions;
        Button Action(string name, string caption, string tooltip, int column, int row, Action? clicked, Button? existing = null, bool wide = false)
        {
            var button = existing ?? new Button(actions, name);
            button.Text = string.Empty;
            button.SetBounds(column * (width + gap), row * (height + gap), wide ? 3 * width + 2 * gap : width, height);
            button.SetToolTipText(tooltip);
            // An explicit font and fixed child label avoid the Empire bitmap
            // button text collapsing to a single glyph after loading legacy UI.
            var label = new Label(button, name + "Caption")
            {
                Font = _detailsButton.Font ?? mCharacterWindow.Skin.DefaultFont,
                FontSize = 9, AutoSizeToContents = false, Text = caption,
                TextColorOverride = Color.White, TextAlign = Pos.Center, MouseInputEnabled = false,
            };
            label.SetBounds(0, 0, button.Width, height); label.BringToFront();
            if (clicked != null) button.Clicked += (_, _) => clicked();
            return button;
        }
        Action(nameof(_detailsButton), Strings.Character.ExtraBuffDetails, Strings.Character.ExtraBuffDetails, 0, 0, null, _detailsButton);
        _achievementsButton = Action(nameof(_achievementsButton), ClientLanguages.Text("Achievements"), ClientLanguages.Text("Achievements"), 1, 0,
            () => PacketSender.SendRequestAchievementState(openWindow: true));
        _petsButton = Action(nameof(_petsButton), ClientLanguages.Text("Pets"), ClientLanguages.Text("Pets"), 2, 0,
            () => PacketSender.SendRequestPetState(openWindow: true));
        Action("CharacterMounts", ClientLanguages.Text("Mounts"), ClientLanguages.Text("Mounts"), 0, 1, () => MountsWindow.Open());
        Action("CharacterJoust", ClientLanguages.Text("Jousting"), ClientLanguages.Text("Royal Jousting"), 1, 1, () => JoustWindow.Open());
        Action("CharacterArchery", ClientLanguages.Text("Archery"), ClientLanguages.Text("Royal Archery Training"), 2, 1, () => ArcheryWindow.Open());
        Action("CharacterMasteries", ClientLanguages.Text("Weapon Masteries"), ClientLanguages.Text("Weapon Masteries"), 0, 2,
            () => GameInterface.OpenWeaponMasteries(), wide: true);
        void LayoutActions()
        {
            // The loaded titlebar can have stale saved bounds until Gwen lays
            // it out. Recompute afterwards rather than enlarging the background.
            var contentHeight = mCharacterWindow.Height - mCharacterWindow.Titlebar.Height;
            var top = Math.Max(statsBottom + 4, Math.Min(preferredTop, contentHeight - footerHeight - 4));
            actions.SetBounds(margin, top, 3 * width + 2 * gap, Math.Max(20, Math.Min(footerHeight, contentHeight - top - 4)));
        }
        mCharacterWindow.AfterLayout += (_, _) => LayoutActions();
        LayoutActions();
    }
}
