using Intersect.Localization;

namespace Intersect.Framework.Core.Tutorials;

/// <summary>
/// Editor-authored advanced combat lessons. These are never inserted into the
/// first-login guide automatically, which avoids blocking older characters
/// when advanced combat or three-hit combos are disabled on a server.
/// </summary>
public static class AdvancedCombatTutorialLessons
{
    private sealed record Lesson(
        string GuidText, TutorialObjective Objective, string Title, string Text);

    private static readonly Lesson[] Lessons =
    [
        new("c1026029-0000-4000-8000-000000000101", TutorialObjective.CombatDodge,
            "Combat drill: dodge",
            "Dodge a monster's attack by pressing Left Shift while moving (default key). Watch your endurance and find a safe tile."),
        new("c1026029-0000-4000-8000-000000000102", TutorialObjective.CombatPerfectParry,
            "Combat drill: perfect parry",
            "Equip a shield and block just as a monster strikes (Q by default) to land a perfect parry."),
        new("c1026029-0000-4000-8000-000000000103", TutorialObjective.CombatCounterattack,
            "Combat drill: counterattack",
            "After a perfect parry, press R (default key) to counterattack before your opening expires."),
        new("c1026029-0000-4000-8000-000000000104", TutorialObjective.CombatFinisher,
            "Combat drill: finisher",
            "Land three consecutive melee hits on the same monster (E by default). The final hit uses endurance."),
    ];

    public static bool IsCombatObjective(TutorialObjective objective) =>
        objective is TutorialObjective.CombatDodge
            or TutorialObjective.CombatPerfectParry
            or TutorialObjective.CombatCounterattack
            or TutorialObjective.CombatFinisher;

    /// <summary>
    /// Unavailable exercises are safe to bypass server-side. An editor can
    /// include these steps without making ordinary onboarding impossible.
    /// </summary>
    public static bool IsAvailable(TutorialObjective objective,
        bool advancedCombatEnabled, bool playerCombosEnabled) =>
        objective switch
        {
            TutorialObjective.CombatDodge or TutorialObjective.CombatPerfectParry
                or TutorialObjective.CombatCounterattack => advancedCombatEnabled,
            TutorialObjective.CombatFinisher => advancedCombatEnabled && playerCombosEnabled,
            _ => true,
        };

    /// <summary>Append translated, stable-ID lesson steps. Repeated clicks are idempotent.</summary>
    public static int AppendTo(TutorialConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Steps is null || configuration.Translations?.Entries is null)
            return 0;

        var added = 0;
        foreach (var lesson in Lessons)
        {
            var id = Guid.Parse(lesson.GuidText);
            if (configuration.Steps.Any(existing => existing.Id == id))
                continue;

            configuration.Steps.Add(new TutorialStep
            {
                Id = id, Title = lesson.Title, Text = lesson.Text,
                Objective = lesson.Objective,
            });

            foreach (var value in new[] { lesson.Title, lesson.Text })
            {
                var key = GameTextCatalog.NormalizeText(value);
                configuration.Translations.Entries[key] =
                    GameTextCatalog.Languages.ToDictionary(lang => lang,
                        lang => TutorialText.Get(value, lang));
            }

            added++;
        }

        return added;
    }
}
