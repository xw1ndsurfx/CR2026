using Intersect.Framework.Core.Archery;

var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
var now = 10000L;
var match = new ArcheryMatch(now);
Check(!match.Input(false, 1, 0, 0, now), "release requires draw");
Check(!match.Input(true, 2, double.NaN, 0, now), "NaN");
Check(!match.Input(true, 2, double.PositiveInfinity, 0, now), "infinity");
Check(!match.Input(true, 2, .51, 0, now), "aim bounds");
Check(match.Input(true, 2, 0, 0, now), "draw");
Check(!match.Input(true, 2, 0, 0, now), "replay");
Check(match.Input(false, 3, 0, 0, now + 149) && match.Phase == ArcheryPhase.Ready && match.Shot == 0, "short click cancels without consuming an arrow");
Check(match.Input(true, 4, 0, 0, now), "draw after cancel");
Check(match.Input(false, 5, 0, 0, now + 600), "release");
Check(Math.Abs(match.Charge - .5) < 1e-8, "charge measured by server");
Check(match.Score == 0 && match.Shot == 0 && match.Phase == ArcheryPhase.Flying, "not scored before impact");
Check(!match.Input(true, 6, 0, 0, now + 601), "cannot draw in flight");
Check(!match.Advance(match.ImpactAt - 1), "flight deadline");
Check(match.Advance(match.ImpactAt), "impact resolved");
Check(match.Shot == 1 && !match.Advance(match.ImpactAt), "impact exactly once");

match = new ArcheryMatch(now); var sequence = 0L;
for (var i = 0; i < 15; i++)
{
    Check(match.Distance == (i < 5 ? 10 : i < 10 ? 25 : 50), "round distance");
    Check(match.Input(true, ++sequence, 0, 0, now), "draw per shot");
    now += 1200;
    var pitch = Math.Asin(ArcheryPhysics.Gravity * match.Distance / (55 * 55)) / 2;
    Check(match.Input(false, ++sequence, -Math.Sin((now - match.Started) / 700d) * .0015,
        pitch - Math.Cos((now - match.Started) / 900d) * .0015, now), "ballistic release");
    now = match.ImpactAt; match.Advance(now);
    Check(match.Impacts[^1].Points == 10, "bullseye at each distance");
    now += 500;
}
Check(match.Score == 150 && match.Phase == ArcheryPhase.Finished, "3 x 5 x 10");
Check(!match.Input(true, ++sequence, 0, 0, now), "cannot score sixteenth arrow");
var before = match.Score; match.Abandon(); Check(match.Score == before, "completed score immutable");
match = new ArcheryMatch(0); match.Input(true, 1, 0, 0, 0); match.Advance(10001);
Check(match.Phase == ArcheryPhase.Abandoned, "draw timeout releases session");
match = new ArcheryMatch(0); match.Advance(300000); Check(match.Done, "session timeout");
match = new ArcheryMatch(0); match.Abandon(); Check(!match.Input(true, 1, 0, 0, 1), "abandon");
Check(ArcheryPhysics.Impact(.4, 0, 1, 0, 10).Points == 0, "miss");
var calm = ArcheryPhysics.Impact(0, .05, 1, 0, 25);
var wind = ArcheryPhysics.Impact(0, .05, 1, 3, 25);
Check(wind.X > calm.X, "wind affects trajectory");
Check(ArcheryPhysics.Project(1, 0, 10).Scale > ArcheryPhysics.Project(1, 0, 50).Scale, "perspective depth");
var config = new RoyalCombatConfiguration(); Check(config.IsValid, "safe default config");
Check(RoyalCombatConfiguration.FromJson(config.ToJson()).IsValid, "config round trip");
var mastery = config.Masteries[0];
Check(mastery.Level(0) == 1 && mastery.Level(99) == 1 && mastery.Level(100) == 2, "XP threshold");
Check(mastery.WithinLevel(105) == 5, "XP within level");
Check(mastery.Level(mastery.Maximum) == 30 && mastery.WithinLevel(mastery.Maximum) == 0, "max cap");
config.Masteries[0].WeaponIds = config.Masteries[1].WeaponIds = [Guid.NewGuid()];
Check(!config.IsValid, "weapon belongs to one mastery");
config = new(); config.Ranges = [new() { Enabled = true }]; Check(!config.IsValid, "range needs real map");
config = new(); config.Masteries[0].DamagePercentPerLevel = double.NaN; Check(!config.IsValid, "finite bonus");
config = new(); config.Masteries[0].Cap = 0; Check(!config.IsValid, "cap bounds");
config = new(); config.Masteries = null!; Check(!config.IsValid, "malformed config fails closed");
var tree = new MasteryCategory { Talents = [new() { DamagePercentPerRank = .5 },
    new() { Key = "quick-shot", Prerequisite = "steady-aim", RequiredLevel = 3, AttackSpeedPercentPerRank = .5 }] };
var ranks = new Dictionary<string, int>();
Check(MasteryRules.ValidTree(tree), "valid talent tree");
Check(!MasteryRules.CanBuy(tree, tree.Talents[0], 0, ranks), "no points at level one");
Check(MasteryRules.CanBuy(tree, tree.Talents[0], 100, ranks), "level provides one point");
ranks["steady-aim"] = 1;
Check(!MasteryRules.CanBuy(tree, tree.Talents[0], 100, ranks), "spent points unavailable");
Check(!MasteryRules.CanBuy(tree, tree.Talents[1], 100, ranks), "level gate");
Check(MasteryRules.CanBuy(tree, tree.Talents[1], 500, ranks), "prerequisite and point available");
tree.Talents[0].Prerequisite = "quick-shot"; Check(!MasteryRules.ValidTree(tree), "cycles rejected");
tree.Talents[0].Prerequisite = "missing"; Check(!MasteryRules.ValidTree(tree), "dangling prerequisite");
tree.Talents[0].Prerequisite = ""; tree.Talents[0].RankSpellIds = [Guid.NewGuid()];
Check(!MasteryRules.ValidTree(tree), "active spell must map every rank");
tree.Talents[0].RankSpellIds = [];
tree.Talents[0].MaxRank = 6; Check(!MasteryRules.ValidTree(tree), "rank cap five");
tree.Talents[0].MaxRank = 5; ranks["steady-aim"] = 5;
Check(!MasteryRules.CanBuy(tree, tree.Talents[0], tree.Maximum, ranks), "max rank never charged");
Check(!MasteryRules.CanBuy(new MasteryCategory(), new MasteryTalent(), 1000, new Dictionary<string, int>()), "unconfigured skill disabled");
config = new(); config.Ammunition = [new() { WeaponId = Guid.NewGuid(), ProjectileIds = [] }]; Check(!config.IsValid, "empty ammo mapping rejected");
var weapon = Guid.NewGuid(); config.Ammunition = [new() { WeaponId = weapon, ProjectileIds = [Guid.NewGuid()] }, new() { WeaponId = weapon, ProjectileIds = [Guid.NewGuid()] }];
Check(!config.IsValid, "one ammo rule per weapon");
ArcheryRuleChecks.Run(Check);
config = new(); config.Ranges = [new() { CompetitionEnabled = true }];
Check(config.IsValid, "default competition rules valid without invented world map");
config.Ranges[0].MinimumPlayers = 1; Check(!config.IsValid, "competition requires two players");
config.Ranges[0].MinimumPlayers = 2; config.Ranges[0].MaximumPlayers = 17; Check(!config.IsValid, "competition capacity sixteen");
config.Ranges[0].MaximumPlayers = 2; config.Ranges[0].RegistrationSeconds = 0; Check(!config.IsValid, "registration cannot have zero deadline");
Console.WriteLine($"PASS: {checks} checks (ballistics, moving targets, timers, zoom, authority, mastery and ammunition).");
