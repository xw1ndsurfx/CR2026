#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames.Progression;
using Microsoft.Data.Sqlite;

namespace Intersect.Server.MiniGames.Currency;

// Reuse ONE inventory/escrow ledger, OS lease, recovery path and transaction boundary.
// Do not instantiate a second ledger for blackjack against the same player database.
public sealed partial class PokerMoneyLedger
{
    public MoneyWin[] SettleBlackjack(Guid table, Guid bankSeat, long hand, IReadOnlyDictionary<Guid, long> closing)
    {
        Required(table); Required(bankSeat);
        if (hand <= 0 || closing.Count is < 2 or > 6 || !closing.ContainsKey(bankSeat)) throw new MoneyRuleException("Invalid blackjack settlement.");
        foreach(var amount in closing.Values) Amount(amount);
        var fingerprint = "blackjack:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";",
            closing.OrderBy(p=>p.Key).Select(p=>Id(p.Key)+":"+p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))))));
        return Write((c,tx)=>
        {
            var old = Scalar(c,tx,"SELECT Fingerprint FROM PokerMoneyHands WHERE TableId=$p0 AND HandId=$p1",Id(table),hand) as string;
            if(old != null) { if(old != fingerprint) throw new MoneyRuleException("Blackjack receipt mismatch."); return Array.Empty<MoneyWin>(); }
            var seats = Seats(c,tx,"TableId=$p0 AND Status=0",Id(table));
            var bank = seats.SingleOrDefault(s=>s.Id==bankSeat);
            if(bank == null || !bank.Npc || !bank.House.StartsWith("blackjack:",StringComparison.Ordinal) ||
                seats.Length!=closing.Count || seats.Any(s=>!closing.ContainsKey(s.Id) || s.House!=bank.House || s.Currency!=bank.Currency) ||
                seats.Sum(s=>s.Amount)!=closing.Values.Sum()) throw new MoneyRuleException("Blackjack funds do not balance.");
            var wins = new List<MoneyWin>();
            foreach(var seat in seats)
            {
                var amount=closing[seat.Id];
                Exec(c,tx,"UPDATE PokerMoneySeats SET Amount=$p1 WHERE Id=$p0",Id(seat.Id),amount);
                if(!seat.Npc && amount>seat.Amount)
                {
                    WriteBlackjackProfile(c,tx,seat.Character,ReadBlackjackProfile(c,tx,seat.Character).WithWin());
                    wins.Add(new(seat.Character,amount-seat.Amount));
                }
            }
            Exec(c,tx,"INSERT INTO PokerMoneyHands VALUES($p0,$p1,$p2,$p3)",Id(table),hand,fingerprint,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Fault?.Invoke("before-blackjack-settlement-commit"); return wins.ToArray();
        });
    }
    private static MiniGameProgress ReadBlackjackProfile(SqliteConnection c,SqliteTransaction? tx,Guid character)
    {
        using var cmd=Command(c,tx,"SELECT Experience,Wins,SelectedBack FROM PokerMoneyProfiles WHERE CharacterId=$p0 AND Game='blackjack'",Id(character));
        using var reader=cmd.ExecuteReader();
        var p=reader.Read()?new MiniGameProgress(reader.GetInt64(0),reader.GetInt64(1),reader.GetInt32(2)):new();
        if(!p.IsValid) throw new MoneyRuleException("Invalid blackjack profile; refusing to reset it.");return p;
    }
    private static void WriteBlackjackProfile(SqliteConnection c,SqliteTransaction tx,Guid character,MiniGameProgress p)=>Exec(c,tx,
        "INSERT INTO PokerMoneyProfiles VALUES($p0,'blackjack',$p1,$p2,$p3) ON CONFLICT(CharacterId,Game) DO UPDATE SET Experience=$p1,Wins=$p2,SelectedBack=$p3;",
        Id(character),p.Experience,p.Wins,p.SelectedBack);
    public MiniGameProgress BlackjackProfile(Guid character)
    { Required(character);lock(_gate){using var c=Open();return ReadBlackjackProfile(c,null,character);} }
    public MiniGameProgress SelectBlackjackBack(Guid character,int back)=>Write((c,tx)=>
    {
        Required(character);var p=ReadBlackjackProfile(c,tx,character);
        if(!MiniGameProgression.IsUnlocked(back,p.Experience))throw new MoneyRuleException("CardBackLocked");
        p=p with{SelectedBack=back};WriteBlackjackProfile(c,tx,character,p);return p;
    });
}
