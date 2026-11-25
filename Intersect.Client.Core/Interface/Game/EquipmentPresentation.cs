using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;

namespace Intersect.Client.Interface.Game;

internal static class EquipmentPresentation
{
    internal static string T(string text) => EquipmentLoadoutWindow.T(text);
    internal static IEnumerable<KeyValuePair<string, string>> Details(ItemDescriptor item, ItemProperties? properties)
    {
        if (item.EffectiveWeaponSpell(properties) is {} binding)
        {
            yield return new(T("Weapon spell"), ClientLanguages.ContentText(SpellDescriptor.Get(binding.SpellId)?.Name ?? T("Missing spell")));
            yield return new(T("Activation"), binding.Trigger == WeaponSpellTrigger.Manual ? T("Manual") + " · " + EquipmentSpellKey() : string.Format(T("On hit ({0}%)"), binding.ChancePercent));
        }
        if (properties?.Enhancement is { EventId: var id } enhancement && id != Guid.Empty)
            yield return new(T("Bound event"), T(enhancement.EventTrigger.ToString()));
    }
    internal static string EquipmentSpellKey()
    {
        if (Intersect.Client.Core.Controls.Controls.ActiveControls.TryGetMappingFor(Intersect.Client.Framework.Input.Control.WeaponSpell, out var mapping) &&
            mapping.Bindings.FirstOrDefault(b => b.Key != Intersect.Client.Framework.GenericClasses.Keys.None) is {} binding)
            return Hotbar.HotBarWindow.NavalKeyCaption(binding);
        return T("Unassigned");
    }
    private static readonly HashSet<string> Cues = ["open", "select", "save", "equip", "delete", "error", "forge", "upgrade", "spell"];
    private static readonly Dictionary<string, long> LastCue = [];
    internal static void Play(string cue)
    {
        if (!Cues.Contains(cue) || Globals.ContentManager == null || Environment.TickCount64 - LastCue.GetValueOrDefault(cue, -10000) < 180) return;
        LastCue[cue] = Environment.TickCount64;
        try { var sound = Intersect.Client.Interface.InterfaceAudio.PlayConfigured("equipment_" + cue + "_v1.wav", false); if (sound is { Loaded: true }) sound.Volume = 48; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException) { }
    }
    internal static ImagePanel Background(Base parent, bool forge, int width, int height)
    {
        var panel = new ImagePanel(parent, "EquipmentArtwork") { MouseInputEnabled = false };
        panel.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, forge ? "equipment_forge_v1.png" : "equipment_loadouts_v1.png");
        panel.SetBounds(0, 0, width, height); return panel;
    }
    internal static ImagePanel Icon(Base parent, int index, int x, int y, int size)
    {
        var icon = new ImagePanel(parent, "EquipmentIcon" + index) { MouseInputEnabled = false };
        icon.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, "equipment_icons_v1.png");
        if (icon.Texture is {} texture) icon.SetTextureRect(index % 2 * texture.Width / 2, index / 2 * texture.Height / 2, texture.Width / 2, texture.Height / 2);
        icon.SetBounds(x, y, size, size); return icon;
    }
}
