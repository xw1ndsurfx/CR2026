using Intersect.Framework.Core.Tutorials;
using Intersect.Localization;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests;

[TestFixture]
public class TutorialCombatLessonsTests
{
    [Test]
    public void CombatLessonsAppendToExistingGuideOnlyWhenEditorRequestsThem()
    {
        var guide = TutorialConfiguration.CreateDefault();
        Assert.That(guide.Steps, Has.Count.EqualTo(9));
        Assert.That(AdvancedCombatTutorialLessons.AppendTo(guide), Is.EqualTo(4));
        Assert.That(guide.Steps, Has.Count.EqualTo(13));
        Assert.That(AdvancedCombatTutorialLessons.AppendTo(guide), Is.Zero);
        Assert.That(guide.Steps, Has.Count.EqualTo(13));
        Assert.That(guide.IsValid, Is.True);
    }

    [Test]
    public void TrainingStepsPersistWithStableIdsAndLocalizedText()
    {
        var guide = TutorialConfiguration.CreateDefault();
        AdvancedCombatTutorialLessons.AppendTo(guide);
        var saved = JsonConvert.DeserializeObject<TutorialConfiguration>(
            JsonConvert.SerializeObject(guide))!;
        Assert.That(saved.IsValid, Is.True);
        var objectives = new[]
        {
            TutorialObjective.CombatDodge, TutorialObjective.CombatPerfectParry,
            TutorialObjective.CombatCounterattack, TutorialObjective.CombatFinisher,
        };
        foreach (var objective in objectives)
        {
            var step = saved.Steps.Single(s => s.Objective == objective);
            Assert.That(step.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(AdvancedCombatTutorialLessons.IsCombatObjective(step.Objective), Is.True);
            foreach (var lang in GameTextCatalog.Languages)
            {
                var title = saved.Translations.Get(step.Title, lang);
                var body = saved.Translations.Get(step.Text, lang);
                Assert.That(title, Is.Not.Empty);
                Assert.That(body, Is.Not.Empty);
                Assert.That(GameTextTokens.Compatible(step.Title, title), Is.True);
                Assert.That(GameTextTokens.Compatible(step.Text, body), Is.True);
            }
        }
    }

    [Test]
    public void DisabledModesCannotBlockOrdinaryOnboarding()
    {
        Assert.That(AdvancedCombatTutorialLessons.IsAvailable(TutorialObjective.Read, false, false), Is.True);
        Assert.That(AdvancedCombatTutorialLessons.IsAvailable(TutorialObjective.CombatDodge, false, false), Is.False);
        Assert.That(AdvancedCombatTutorialLessons.IsAvailable(TutorialObjective.CombatCounterattack, true, false), Is.True);
        Assert.That(AdvancedCombatTutorialLessons.IsAvailable(TutorialObjective.CombatFinisher, true, false), Is.False);
        Assert.That(AdvancedCombatTutorialLessons.IsAvailable(TutorialObjective.CombatFinisher, true, true), Is.True);
    }
}
