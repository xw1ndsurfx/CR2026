namespace Intersect.Framework.Core.Fishing;

/// <summary>Manifest events and stable runtime aliases; absent overrides inherit, empty files are silent.</summary>
public static class FishingSoundCatalog
{
    private static readonly Dictionary<string, string> Bindings = new(StringComparer.Ordinal)
    {
        ["ui_open"] = "ui_open",
        ["ui_close"] = "ui_close",
        ["ui_select"] = "ui_select",
        ["ui_confirm"] = "ui_confirm",
        ["ui_error"] = "ui_error",
        ["cast_rod"] = "cast_rod",
        ["cast_net"] = "cast_net",
        ["cast_cage"] = "cast_cage",
        ["splash"] = "splash",
        ["bite"] = "bite",
        ["reel_loop"] = "reel_loop",
        ["tension_loop"] = "tension_loop",
        ["line_break"] = "line_break",
        ["escape"] = "escape",
        ["catch"] = "catch",
        ["crate_raise"] = "crate_raise",
        ["net_retrieve"] = "net_retrieve",
        ["skill_stabilize"] = "skill_stabilize",
        ["skill_pull"] = "skill_pull",
        ["skill_repair"] = "skill_repair",
        ["skill_sink"] = "skill_sink",
        ["auto_start"] = "auto_start",
        ["auto_stop"] = "auto_stop",
        ["auto_pause"] = "auto_pause",
        ["legendary_appear"] = "legendary_appear",
        ["legendary_challenge_cue"] = "legendary_challenge_cue",
        ["legendary_challenge_success"] = "legendary_challenge_success",
        ["legendary_challenge_fail"] = "legendary_challenge_fail",
        ["legendary_catch"] = "legendary_catch",
        ["xp_gain"] = "xp_gain",
        ["level_up"] = "level_up",
        ["ambience_sea_loop"] = "ambience_sea_loop",
        ["session_start"] = "ui_open",
        ["session_end"] = "ui_close",
        ["session_interrupted"] = "auto_stop",
        ["tool_changed"] = "ui_select",
        ["tool_missing"] = "ui_error",
        ["tool_incompatible"] = "ui_error",
        ["bait_missing"] = "ui_error",
        ["inventory_full"] = "ui_error",
        ["zone_unavailable"] = "ui_error",
        ["legendary_hooked"] = "legendary_appear",
        ["challenge_timing_prompt"] = "legendary_challenge_cue",
        ["challenge_direction_prompt"] = "legendary_challenge_cue",
        ["challenge_reel_prompt"] = "legendary_challenge_cue",
        ["legendary_timeout"] = "legendary_challenge_fail",
        ["legendary_escape"] = "escape",
        ["auto_bait_empty"] = "auto_pause",
        ["auto_inventory_full"] = "auto_pause",
        ["auto_legendary_wait"] = "auto_pause",
        ["reward_item"] = "catch",
        ["reward_xp"] = "xp_gain",
        ["rod_cast"] = "cast_rod",
        ["net_cast"] = "cast_net",
        ["cage_cast"] = "cast_cage",
        ["cage_retrieve"] = "crate_raise",
        ["line_tension_enter"] = "tension_loop",
        ["reeling_start"] = "reel_loop",
        ["reeling_stop"] = "", ["line_tension_exit"] = "",
    };
    public static readonly string[] Keys = [.. Bindings.Keys, "cast", "reel", "tension", "ambience", "legendary", "challenge", "challenge_success", "challenge_fail"];

    public static string Canonical(string key, FishingToolType tool = FishingToolType.Rod) => key switch
    {
        "cast" => tool switch { FishingToolType.Cage => "cast_cage", FishingToolType.Net => "cast_net", _ => "cast_rod" },
        "reel" => "reel_loop", "reeling_start" => "reel_loop",
        "tension" => "tension_loop", "line_tension_enter" => "tension_loop",
        "ambience" => "ambience_sea_loop", "legendary" => "legendary_appear",
        "challenge" => "legendary_challenge_cue", "challenge_success" => "legendary_challenge_success",
        "challenge_fail" => "legendary_challenge_fail",
        _ => Bindings.GetValueOrDefault(key, key),
    };

    public static FishingSoundCue Default(string key, FishingToolType tool = FishingToolType.Rod)
    {
        var file = Canonical(key, tool);
        var looping = file is "reel_loop" or "tension_loop" or "ambience_sea_loop";
        return new FishingSoundCue
        {
            // Cast recordings already include water contact; a separately authored splash remains optional.
            File = string.IsNullOrEmpty(file) || key == "splash" ? "" : $"fishing/fishing_{file}.wav",
            Volume = file == "ambience_sea_loop" ? 25 : looping ? 45 : 75,
            Loop = looping, CooldownMs = 180, MaximumInstances = looping ? 1 : 2,
        };
    }

    public static FishingSoundCue Resolve(FishingConfiguration config, FishingZoneDefinition? zone,
        FishingToolDefinition? tool, FishingCatchDefinition? catchDefinition, string key)
    {
        var canonical = Canonical(key, tool?.Type ?? FishingToolType.Rod);
        foreach (var scope in new[] { catchDefinition?.Sounds, tool?.Sounds, zone?.Sounds, config.Sounds })
        {
            if (scope == null) continue;
            if (scope.Cues.TryGetValue(key, out var exact)) return exact;
            if (scope.Cues.TryGetValue(canonical, out var family)) return family;
            foreach (var runtimeFamily in new[] { "cast", "reel", "tension", "ambience", "legendary", "challenge", "challenge_success", "challenge_fail" })
                if (Canonical(runtimeFamily, tool?.Type ?? FishingToolType.Rod) == canonical && scope.Cues.TryGetValue(runtimeFamily, out var inheritedFamily))
                    return inheritedFamily;
            // Generic server families inherit a more precise authored event where it exists.
            if (key is "cast" or "reel" or "tension" or "ambience" or "legendary" or "challenge" or "challenge_success" or "challenge_fail")
                foreach (var alias in Bindings.Where(pair => pair.Value == canonical && pair.Key is not ("challenge_timing_prompt" or "challenge_reel_prompt")))
                    if (scope.Cues.TryGetValue(alias.Key, out var aliased)) return aliased;
        }
        return Default(key, tool?.Type ?? FishingToolType.Rod);
    }
}
