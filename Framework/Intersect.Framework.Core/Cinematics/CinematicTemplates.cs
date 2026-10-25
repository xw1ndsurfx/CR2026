namespace Intersect.Framework.Core.Cinematics;

public enum CinematicShotPreset { WideShot, MediumShot, CloseUp, HeroShot, BossReveal, EstablishingShot, BattleOverview }
public enum CinematicTemplateKind
{
    GameIntroduction, CityIntroduction, QuestStart, RoyalAudience, BossEncounter, Victory, Defeat,
    Flashback, MagicalVision, NavalBattle, Battlefield, Invasion, ChapterConclusion,
}

public static class CinematicTemplates
{
    public static void ApplyShot(CinematicClip clip, CinematicShotPreset preset)
    {
        if (clip.Track != CinematicTrack.Camera || !Enum.IsDefined(preset)) throw new ArgumentException(nameof(preset));
        clip.OffsetX = 0; clip.OffsetY = 0; clip.FollowActor = false;
        clip.Zoom = preset switch
        {
            CinematicShotPreset.CloseUp => 3,
            CinematicShotPreset.HeroShot or CinematicShotPreset.BossReveal => 2,
            CinematicShotPreset.MediumShot => 1.5f,
            CinematicShotPreset.BattleOverview => .5f,
            _ => .75f,
        };
        if (preset is CinematicShotPreset.HeroShot or CinematicShotPreset.BossReveal)
        { clip.OffsetY = -1; clip.FollowActor = true; }
    }

    // Templates are editable native scenes, not movies or permanent lore. No asset IDs,
    // rewards, NPC spawns or gameplay commands are fabricated by this factory.
    public static CinematicScene Create(CinematicTemplateKind kind, Guid map, int width, int height, Func<string, string> text)
    {
        if (!Enum.IsDefined(kind) || map == Guid.Empty || width <= 0 || height <= 0) throw new ArgumentException(nameof(kind));
        var x = width / 2; var y = height / 2;
        var scene = new CinematicScene { MapId = map, Name = text("Template" + kind), DurationMs = 12000 };
        scene.Actors.Add(new() { Name = text("Player"), StartX = x, StartY = y });
        var wide = kind is CinematicTemplateKind.NavalBattle or CinematicTemplateKind.Battlefield or CinematicTemplateKind.Invasion
            ? CinematicShotPreset.BattleOverview : CinematicShotPreset.EstablishingShot;
        var opening = new CinematicClip { Track = CinematicTrack.Camera, Name = text(wide.ToString()), X = x, Y = y, DurationMs = 3500 };
        ApplyShot(opening, wide); scene.Clips.Add(opening);
        var close = new CinematicClip { Track = CinematicTrack.Camera, Name = text("MediumShot"), X = x, Y = y, StartMs = 3500, DurationMs = 4500 };
        ApplyShot(close, kind == CinematicTemplateKind.BossEncounter ? CinematicShotPreset.BossReveal : CinematicShotPreset.MediumShot);
        scene.Clips.Add(close);
        scene.Clips.Add(new() { Track = CinematicTrack.Camera, Name = text("WideShot"), X = x, Y = y, Zoom = 1, StartMs = 8000, DurationMs = 3000 });
        scene.Clips.Add(new() { Track = CinematicTrack.Presentation, Name = text("Presentation"), Letterbox = .1f, HideHud = true, DurationMs = 800 });
        if (kind is CinematicTemplateKind.Flashback or CinematicTemplateKind.MagicalVision or CinematicTemplateKind.Defeat)
        {
            scene.Clips.Add(new() { Track = CinematicTrack.Presentation, Name = text("FadeOpacity"), Letterbox = .1f,
                HideHud = true, FadeOpacity = 1, StartMs = 8500, DurationMs = 1000 });
            scene.Clips.Add(new() { Track = CinematicTrack.Presentation, Name = text("FadeOpacity"), Letterbox = .1f,
                HideHud = true, StartMs = 9500, DurationMs = 1000 });
        }
        scene.Clips.Add(new() { Track = CinematicTrack.Presentation, Name = text("Presentation"), StartMs = 11000, DurationMs = 1000 });
        return scene;
    }
}
