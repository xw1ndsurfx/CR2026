using Intersect.Client.Core;
using Intersect.Client.Core.Sounds;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;

namespace Intersect.Client.Interface;

/// <summary>Original fallback palette shared by main-menu, game and modal interfaces.</summary>
public static class InterfaceAudio
{
    public static void Configure(Canvas canvas)
    {
        canvas.InterfaceSoundsEnabled = true;
        canvas.InterfaceWindowSoundResolver = ResolveWindow;
    }

    public static UiWindowSounds? ResolveWindow(Base window)
    {
        var name = window.GetType().Name + "/" + window.Name;
        bool Has(params string[] values) => values.Any(value => name.Contains(value, StringComparison.OrdinalIgnoreCase));
        // Persistent HUDs, hover descriptions and debug tools are not window transitions.
        if (Has("Hud", "HotBar", "Chatbox", "ChatWindow", "Description", "Debug", "EventDialogFace")) return null;
        if (window is Shared.AlertWindow alert) return alert.InterfaceWindowSounds;
        if (window is Shared.InputBox || Has("Confirmation", "Invitation", "QuestOffer")) return new(UiCue.Prompt);
        if (Has("Reward", "Achievement", "Completion")) return new(UiCue.Reward);
        if (Has("Alert", "Notification")) return new(UiCue.Notice);
        if (Has("MainMenu", "Login", "Registration", "SelectCharacter", "CharacterCreation", "Password")) return new(UiCue.MenuOpen);
        if (Has("Bank", "Cargo", "Chest")) return new(UiCue.ChestOpen, UiCue.ChestClose);
        if (Has("Shop", "Market", "Trading")) return new(UiCue.Coins, UiCue.ChestClose);
        if (Has("Inventory", "Bag")) return new(UiCue.BagOpen);
        if (Has("Equipment", "CharacterWindow", "CharacterDetails", "Mastery", "Masteries", "Stylist")) return new(UiCue.Armor);
        if (Has("Spell", "Potion", "Alchemy")) return new(UiCue.Magic, UiCue.BookClose);
        if (Has("Quest", "Contract", "Profession", "Season", "News", "Credits", "Tutorial", "Dungeon")) return new(UiCue.BookOpen, UiCue.BookClose);
        if (Has("Map", "Naval", "Harbour", "Expedition")) return new(UiCue.Map);
        if (Has("Guild", "Headquarters", "Friend", "Party", "Lobby", "Profile", "Pets", "Mount")) return new(UiCue.Social);
        if (Has("Craft", "Cooking", "Metalwork", "Repair", "Housing", "Farm", "Furniture")) return new(UiCue.Workshop);
        return new(UiCue.PanelOpen);
    }

    /// <summary>Keep existing authored UI effects; suppress an overlapping fallback only if playback loaded.</summary>
    public static Sound? PlayConfigured(string file, bool loop)
    {
        var sound = Audio.AddGameSound(file, loop);
        if (!loop && sound is { Loaded: true })
        {
            Interface.NotifyDedicatedInterfaceSound();
        }
        return sound;
    }
}
