using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Server.MiniGames.Currency;

internal static class DiceMoneyTests
{
    internal static void Run()
    {
        var rules = new DiceRules(25, 100, 25);
        var counts = new int[3];
        for (var first = 1; first <= 6; first++)
        for (var second = 1; second <= 6; second++)
        {
            var category = DiceRules.Category(first, second); counts[(int)category]++;
            foreach (var bet in Enum.GetValues<DiceBet>())
                Check(rules.Return(bet, 100, first, second) == (category == bet ? 100 * rules.Multiplier(bet) : 0), "36-pair payout");
        }
        Check(counts.SequenceEqual(new[] { 15, 6, 15 }), "15/6/15 categories");
        Check(rules.Return(DiceBet.OverSeven, 100, 4, 5) - 100 == 100, "4+5 example");
        Check(DiceRules.Category(1, 5) == DiceBet.UnderSeven && DiceRules.Category(1, 6) == DiceBet.Seven &&
            DiceRules.Category(2, 6) == DiceBet.OverSeven, "6/7/8 boundaries");
        Check(new long[] { long.MinValue, -25, 0, 24, 26, 125, long.MaxValue }.All(n => !rules.Accepts(n)), "invalid wagers");
        Check(!new DiceRules(25, 100, 0).IsValid && !new DiceRules(25, 100, 10).IsValid &&
            !new DiceRules(25, 100, 25, int.MaxValue).IsValid, "invalid configuration");
        foreach (var level in Enumerable.Range(1, 25))
        {
            Check(MiniGameProgression.Level(MiniGameProgression.ExperienceAtLevel(level)) == level, "level threshold");
            if (level > 1) Check(MiniGameProgression.Level(MiniGameProgression.ExperienceAtLevel(level) - 1) == level - 1, "level boundary");
        }
        Check(new Intersect.Server.MiniGames.Progression.MiniGameProgress(29990).WithExperience(50).Experience == 30000, "XP cap");
        Check(new Intersect.Server.MiniGames.Progression.MiniGameProgress().WithExperience(5000).Level == 10, "multiple level-ups");
        Console.WriteLine("PASS dice: all 36 outcomes, boundaries, example, wager limits, level thresholds");
        Test("atomic result, duplicate replay, separate XP and restart recovery", f =>
        {
            var (human, bank) = Seats(f);
            var receipt = f.Money.SettleDice(f.TableId, human.Id, bank.Id, 1, DiceBet.OverSeven, 100, rules);
            var xp = receipt.Return > 0 ? 25 : 0;
            Check(f.Amount(human.Id) == receipt.Balance && f.Amount(bank.Id) == receipt.Bank, "escrow result");
            Check(receipt.Balance + receipt.Bank == 10_100 && receipt.ExperienceAwarded == xp, "conservation / XP");
            Check(f.Money.Profile(f.A).Experience == 0 && f.Money.DiceProfile(f.A).Experience == xp, "separate progression");
            Parallel.For(0, 10, _ => Check(f.Money.SettleDice(f.TableId, human.Id, bank.Id, 1, DiceBet.OverSeven, 100, rules) == receipt, "replayed receipt"));
            Throws(() => f.Money.SettleDice(f.TableId, human.Id, bank.Id, 1, DiceBet.Seven, 100, rules));
            Check(f.Money.DiceProfile(f.A).Experience == xp && f.Count("DiceRounds") == 1, "duplicate reward");
            f.Restart();
            Check(f.Money.LastDiceRound(f.A, "dice:test") == receipt && f.Money.DiceProfile(f.A).Experience == xp, "durable outcome");
            f.RefundAll(f.A); f.RefundAll(f.A);
            Check(f.Balance(f.A) == 250 + receipt.Balance, "single recovery refund");
        });
        Test("rejections and pre-commit failure leave all money and XP intact", f =>
        {
            var (human, bank) = Seats(f);
            _ = f.Money.LastDiceRound(f.A, "dice:test");
            foreach (var value in new long[] { -1, 0, 24, 26, 125, long.MaxValue })
                Throws(() => f.Money.SettleDice(f.TableId, human.Id, bank.Id, 1, DiceBet.Seven, value, rules));
            Throws(() => f.Money.SettleDice(f.TableId, human.Id, bank.Id, 2, DiceBet.Seven, 100, rules));
            f.Money.Fault = p => { if (p == "before-dice-settlement-commit") throw new IOException("disk failure"); };
            Throws(() => f.Money.SettleDice(f.TableId, human.Id, bank.Id, 1, DiceBet.Seven, 100, rules));
            Check(f.Amount(human.Id) == 100 && f.Amount(bank.Id) == 10000 && f.Count("DiceRounds") == 0 && f.Money.DiceProfile(f.A).Experience == 0, "partial settlement");
            f.Money.Fault = null;
            var receipt = f.Money.SettleDice(f.TableId, human.Id, bank.Id, 1, DiceBet.Seven, 100, rules, 50);
            Check(receipt.ExperienceAwarded == (receipt.Return > 0 ? 50 : 0), "event XP bonus");
        });
        Test("different players have independent revisions and private balances", f =>
        {
            var a = f.Buy(f.A, 100, f.TableId, "dice:test");
            var bankA = f.Money.OpenNpc(Guid.NewGuid(), f.TableId, f.Currency, "dice:test", 20000, 10000)!;
            var tableB = Guid.NewGuid(); var b = f.Buy(f.B, 100, tableB, "dice:test");
            var bankB = f.Money.OpenNpc(Guid.NewGuid(), tableB, f.Currency, "dice:test", 20000, 1000)!;
            var first = f.Money.SettleDice(f.TableId, a.Id, bankA.Id, 1, DiceBet.UnderSeven, 25, rules);
            Check(f.Amount(b.Id) == 100 && f.Money.DiceProfile(f.B).Experience == 0, "peer changed another wallet");
            var second = f.Money.SettleDice(tableB, b.Id, bankB.Id, 1, DiceBet.Seven, 25, rules);
            Check(first.RoundId != second.RoundId && f.Amount(a.Id) == first.Balance && f.Amount(b.Id) == second.Balance, "independent results");
            Throws(() => f.Money.SettleDice(f.TableId, b.Id, bankB.Id, 1, first.Bet, first.Wager, rules));
            Throws(() => f.Money.SettleDice(f.TableId, b.Id, bankB.Id, 2, DiceBet.Seven, 25, rules));
        });
    }
    private static (MoneySeat, MoneySeat) Seats(Fixture f)
    {
        var human = f.Buy(f.A, 100, f.TableId, "dice:test");
        return (human, f.Money.OpenNpc(Guid.NewGuid(), f.TableId, f.Currency, "dice:test", 10000, 10000)!);
    }
    private static void Test(string name, Action<Fixture> action)
    { using var f = new Fixture(); action(f); Console.WriteLine("PASS dice: " + name); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
}
