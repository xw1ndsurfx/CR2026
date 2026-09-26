#nullable enable
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames.Progression;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace Intersect.Server.MiniGames.Currency;

// Roulette reuses the existing escrow tables, OS lease, refund pipeline and profile table.
public sealed partial class PokerMoneyLedger
{
    public MoneyWin[] SettleRoulette(
        Guid table,
        Guid bankSeat,
        long spin,
        IReadOnlyDictionary<Guid, long> closing
    )
    {
        Required(table);
        Required(bankSeat);
        if (spin <= 0 || closing.Count != 2 || !closing.ContainsKey(bankSeat))
            throw new MoneyRuleException("Invalid roulette settlement.");

        foreach (var amount in closing.Values) Amount(amount);

        var fingerprint = "roulette:" + Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    string.Join(
                        ";",
                        closing.OrderBy(pair => pair.Key)
                            .Select(pair => Id(pair.Key) + ":" + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    )
                )
            )
        );

        return Write((connection, transaction) =>
        {
            var old = Scalar(
                connection,
                transaction,
                "SELECT Fingerprint FROM PokerMoneyHands WHERE TableId=$p0 AND HandId=$p1",
                Id(table),
                spin
            ) as string;

            if (old != null)
            {
                if (old != fingerprint)
                    throw new MoneyRuleException("Roulette receipt mismatch.");

                return Array.Empty<MoneyWin>();
            }

            var seats = Seats(connection, transaction, "TableId=$p0 AND Status=0", Id(table));
            var bank = seats.SingleOrDefault(seat => seat.Id == bankSeat);

            if (bank == null ||
                !bank.Npc ||
                !bank.House.StartsWith("roulette:", StringComparison.Ordinal) ||
                seats.Length != 2 ||
                seats.Any(seat =>
                    !closing.ContainsKey(seat.Id) ||
                    seat.House != bank.House ||
                    seat.Currency != bank.Currency
                ) ||
                seats.Sum(seat => seat.Amount) != closing.Values.Sum())
            {
                throw new MoneyRuleException("Roulette funds do not balance.");
            }

            var wins = new List<MoneyWin>();
            foreach (var seat in seats)
            {
                var amount = closing[seat.Id];
                Exec(
                    connection,
                    transaction,
                    "UPDATE PokerMoneySeats SET Amount=$p1 WHERE Id=$p0",
                    Id(seat.Id),
                    amount
                );

                if (!seat.Npc && amount > seat.Amount)
                {
                    WriteRouletteProfile(
                        connection,
                        transaction,
                        seat.Character,
                        ReadRouletteProfile(connection, transaction, seat.Character).WithWin()
                    );
                    wins.Add(new MoneyWin(seat.Character, amount - seat.Amount));
                }
            }

            Exec(
                connection,
                transaction,
                "INSERT INTO PokerMoneyHands VALUES($p0,$p1,$p2,$p3)",
                Id(table),
                spin,
                fingerprint,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );

            Fault?.Invoke("before-roulette-settlement-commit");
            return wins.ToArray();
        });
    }

    private static MiniGameProgress ReadRouletteProfile(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid character
    )
    {
        using var command = Command(
            connection,
            transaction,
            "SELECT Experience,Wins,SelectedBack FROM PokerMoneyProfiles WHERE CharacterId=$p0 AND Game='roulette'",
            Id(character)
        );
        using var reader = command.ExecuteReader();

        var profile = reader.Read()
            ? new MiniGameProgress(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2))
            : new MiniGameProgress();

        if (!profile.IsValid)
            throw new MoneyRuleException("Invalid roulette profile; refusing to reset it.");

        return profile;
    }

    private static void WriteRouletteProfile(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid character,
        MiniGameProgress profile
    ) =>
        Exec(
            connection,
            transaction,
            "INSERT INTO PokerMoneyProfiles VALUES($p0,'roulette',$p1,$p2,$p3) " +
            "ON CONFLICT(CharacterId,Game) DO UPDATE SET Experience=$p1,Wins=$p2,SelectedBack=$p3;",
            Id(character),
            profile.Experience,
            profile.Wins,
            profile.SelectedBack
        );

    public MiniGameProgress RouletteProfile(Guid character)
    {
        Required(character);
        lock (_gate)
        {
            using var connection = Open();
            return ReadRouletteProfile(connection, null, character);
        }
    }
}
