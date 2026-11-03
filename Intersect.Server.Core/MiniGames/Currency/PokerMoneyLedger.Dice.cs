#nullable enable
using System.Security.Cryptography;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Server.MiniGames.Progression;
using Microsoft.Data.Sqlite;

namespace Intersect.Server.MiniGames.Currency;

public sealed record DiceReceipt(Guid RoundId, Guid Character, string House, Guid Table, long Revision,
    DiceBet Bet, long Wager, int First, int Second, long Return, long Balance, long Bank, long ExperienceAwarded);

public sealed partial class PokerMoneyLedger
{
    // Additive schema in the same player database as escrow and progression. No cross-database commit.
    private static void EnsureDiceSchema(SqliteConnection c, SqliteTransaction tx) => Exec(c, tx, """
        CREATE TABLE IF NOT EXISTS DiceRounds (
            RoundId TEXT PRIMARY KEY, CharacterId TEXT NOT NULL, House TEXT NOT NULL,
            TableId TEXT NOT NULL, Revision INTEGER NOT NULL,
            Bet INTEGER NOT NULL, Wager INTEGER NOT NULL, First INTEGER NOT NULL, Second INTEGER NOT NULL,
            ReturnAmount INTEGER NOT NULL, Balance INTEGER NOT NULL, Bank INTEGER NOT NULL,
            ExperienceAwarded INTEGER NOT NULL, Created INTEGER NOT NULL,
            UNIQUE(TableId, Revision));
        CREATE INDEX IF NOT EXISTS DiceCharacterRounds ON DiceRounds(CharacterId,House,Created);
        """);

    private static DiceReceipt? ReadDiceReceipt(SqliteConnection c, SqliteTransaction tx, string where, params object[] values)
    {
        using var cmd = Command(c, tx, "SELECT RoundId,CharacterId,House,TableId,Revision,Bet,Wager,First,Second," +
            "ReturnAmount,Balance,Bank,ExperienceAwarded FROM DiceRounds WHERE " + where, values);
        using var reader = cmd.ExecuteReader();
        return !reader.Read() ? null : new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
            reader.GetString(2), Guid.Parse(reader.GetString(3)), reader.GetInt64(4), (DiceBet)reader.GetInt32(5),
            reader.GetInt64(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt64(9), reader.GetInt64(10),
            reader.GetInt64(11), reader.GetInt64(12));
    }

    public DiceReceipt? LastDiceRound(Guid character, string house) => Write((c, tx) =>
    {
        Required(character);
        EnsureDiceSchema(c, tx);
        return ReadDiceReceipt(c, tx, "CharacterId=$p0 AND House=$p1 ORDER BY Created DESC,rowid DESC LIMIT 1", Id(character), house);
    });

    private static MiniGameProgress ReadDiceProfile(SqliteConnection c, SqliteTransaction? tx, Guid character)
    {
        using var cmd = Command(c, tx, "SELECT Experience,Wins,SelectedBack FROM PokerMoneyProfiles " +
            "WHERE CharacterId=$p0 AND Game='roll_the_dice'", Id(character));
        using var reader = cmd.ExecuteReader();
        var profile = reader.Read() ? new MiniGameProgress(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2)) : new();
        if (!profile.IsValid) throw new MoneyRuleException("Invalid dice progression; refusing to reset it.");
        return profile;
    }

    public MiniGameProgress DiceProfile(Guid character)
    {
        Required(character);
        lock (_gate) { using var c = Open(); return ReadDiceProfile(c, null, character); }
    }

    /// <summary>
    /// The two draws, escrow balances, final outcome, XP and unique revision receipt commit together.
    /// A repeated request returns its original result without drawing or awarding again.
    /// </summary>
    public DiceReceipt SettleDice(Guid table, Guid humanSeat, Guid bankSeat, long revision,
        DiceBet bet, long wager, DiceRules rules, long winExperience = MiniGameProgression.ExperiencePerWin)
    {
        Required(table); Required(humanSeat); Required(bankSeat);
        if (revision <= 0 || !rules.Accepts(wager) || !Enum.IsDefined(bet) || winExperience is < 0 or > 30_000)
            throw new MoneyRuleException("InvalidBet");
        return Write((c, tx) =>
        {
            EnsureDiceSchema(c, tx);
            var old = ReadDiceReceipt(c, tx, "TableId=$p0 AND Revision=$p1", Id(table), revision);
            if (old != null)
            {
                if (old.Bet != bet || old.Wager != wager) throw new MoneyRuleException("StaleState");
                var owner = Seats(c, tx, "Id=$p0", Id(humanSeat)).SingleOrDefault();
                var originalBank = Seats(c, tx, "Id=$p0", Id(bankSeat)).SingleOrDefault();
                if (owner == null || owner.Npc || owner.Character != old.Character || owner.Table != table ||
                    originalBank == null || !originalBank.Npc || originalBank.Table != table ||
                    owner.Currency != originalBank.Currency || owner.House != old.House || originalBank.House != old.House)
                    throw new MoneyRuleException("StaleState");
                return old;
            }
            var seats = Seats(c, tx, "TableId=$p0 AND Status=0", Id(table));
            var human = seats.SingleOrDefault(s => s.Id == humanSeat && !s.Npc);
            var bank = seats.SingleOrDefault(s => s.Id == bankSeat && s.Npc);
            if (seats.Length != 2 || human == null || bank == null || human.House != bank.House ||
                human.Currency != bank.Currency || !human.House.StartsWith("dice:", StringComparison.Ordinal))
                throw new MoneyRuleException("StaleState");
            var previous = Convert.ToInt64(Scalar(c, tx, "SELECT COALESCE(MAX(Revision),0) FROM DiceRounds WHERE TableId=$p0", Id(table)));
            if (revision != previous + 1) throw new MoneyRuleException("StaleState");
            if (wager > human.Amount) throw new MoneyRuleException("NotEnoughChips");
            var liability = checked(wager * (rules.Multiplier(bet) - 1L));
            if (liability > bank.Amount) throw new MoneyRuleException("BankTooLow");
            // Check every possible balance before drawing, not just the eventual outcome.
            if (human.Amount > MaximumBalance - liability || bank.Amount > MaximumBalance - wager)
                throw new MoneyRuleException("InvalidBet");
            var first = RandomNumberGenerator.GetInt32(1, 7);
            var second = RandomNumberGenerator.GetInt32(1, 7);
            var returned = rules.Return(bet, wager, first, second);
            var net = returned - wager;
            var profile = ReadDiceProfile(c, tx, human.Character);
            var nextProfile = returned > 0 ? profile.WithExperience(winExperience) : profile;
            var receipt = new DiceReceipt(Guid.NewGuid(), human.Character, human.House, table, revision,
                bet, wager, first, second, returned, checked(human.Amount + net), checked(bank.Amount - net),
                nextProfile.Experience - profile.Experience);
            Exec(c, tx, "UPDATE PokerMoneySeats SET Amount=$p1 WHERE Id=$p0", Id(humanSeat), receipt.Balance);
            Exec(c, tx, "UPDATE PokerMoneySeats SET Amount=$p1 WHERE Id=$p0", Id(bankSeat), receipt.Bank);
            Exec(c, tx, "INSERT INTO PokerMoneyProfiles VALUES($p0,'roll_the_dice',$p1,$p2,$p3) " +
                "ON CONFLICT(CharacterId,Game) DO UPDATE SET Experience=$p1,Wins=$p2,SelectedBack=$p3",
                Id(human.Character), nextProfile.Experience, nextProfile.Wins, nextProfile.SelectedBack);
            Exec(c, tx, "INSERT INTO DiceRounds VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8,$p9,$p10,$p11,$p12,$p13)",
                Id(receipt.RoundId), Id(human.Character), human.House, Id(table), revision, (int)bet, wager,
                first, second, returned, receipt.Balance, receipt.Bank, receipt.ExperienceAwarded, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Fault?.Invoke("before-dice-settlement-commit");
            return receipt;
        });
    }
}
