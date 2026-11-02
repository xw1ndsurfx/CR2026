using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.PartyLobbies;
using Intersect.Network.Packets.Server;
using MessagePack;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using Console = System.Console;

internal static class DiceSceneTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _ = new TestContentManager();
        var assembly = typeof(GameInterface).Assembly;
        var windowType = assembly.GetType("Intersect.Client.Interface.Game.DiceWindow", true)!;
        var modelType = assembly.GetType("Intersect.Client.MiniGames.DiceClientModel", true)!;
        var player = Guid.NewGuid();
        var packet = new DiceStatePacket { TableInstanceId = Guid.NewGuid(), ViewId = Guid.NewGuid(), PlayerId = player,
            Sequence = 1, TableName = "dice-1", State = new DiceTableState { Balance = 1100, Bank = 10000,
                MinimumBet = 25, MaximumBet = 1000, Step = 25, OutsideReturn = 2, SevenReturn = 5,
                RoundId = Guid.NewGuid(), First = 4, Second = 5, LastBet = DiceBet.OverSeven,
                Wager = 100, Return = 200, Experience = 125, ExperienceAwarded = 25, CurrencyItemId = Guid.NewGuid(),
                Participants = Enumerable.Range(0, 6).Select(i => new DiceParticipant { PlayerId = i == 0 ? player : Guid.NewGuid(),
                    Name = "Player " + i, RoundId = Guid.NewGuid(), First = i + 1, Second = 6, Wager = 25, Return = 50 }).ToArray() } };
        Check(packet.IsValid, "valid six-player state");
        var copy = MessagePackSerializer.Deserialize<DiceStatePacket>(MessagePackSerializer.Serialize(packet));
        Check(copy.IsValid && copy.State!.Participants.Length == 6 && copy.State.First == 4, "protocol round trip");
        foreach (var size in new[] { new Point(1280, 720), new Point(1920, 1080) })
        {
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = size };
            var scene = (ControlBase)Activator.CreateInstance(windowType, [canvas, new Action<DiceRequestKind, long, DiceBet, int>((_, _, _, _) => { })])!;
            try
            {
                var model = Activator.CreateInstance(modelType)!;
                bool Apply() => (bool)modelType.GetMethod("Apply")!.Invoke(model, [packet, player, 1000L])!;
                Check(Apply(), "first snapshot"); windowType.GetMethod("Update")!.Invoke(scene, [model]);
                var labels = scene.Children.OfType<Label>().ToArray();
                Check(labels.Single(l => l.Name == "DiceTotal").Text.Contains("4 + 5 = 9"), "server dice faces");
                Check(labels.Single(l => l.Name == "DiceLast").Text.Contains("200") && labels.Single(l => l.Name == "DiceLast").Text.Contains("+100"), "stake / return / net");
                Check(labels.Single(l => l.Name == "DiceExperience").Text == "25 / 200 XP", "real level-relative thresholds");
                Check(labels.Count(l => l.Name.StartsWith("DicePlayer") && !string.IsNullOrEmpty(l.Text)) == 7, "all six participants plus heading");
                foreach (var control in scene.Children.Where(c => !c.IsHidden))
                    Check(control.X >= 0 && control.Y >= 0 && control.X + control.Width <= size.X && control.Y + control.Height <= size.Y, "bounds: " + control.Name);
                var request = (DiceRequestPacket)modelType.GetMethod("Request")!.Invoke(model, [DiceRequestKind.Roll, 1100L, 100L, DiceBet.OverSeven, -1])!;
                Check(request.IsValid && request.Revision == packet.State!.Revision, "request revision");
                Check(modelType.GetMethod("Request")!.Invoke(model, [DiceRequestKind.Roll, 1101L, 100L, DiceBet.OverSeven, -1]) == null, "double click guard");
                packet.Sequence++; packet.RequestId = request.RequestId; packet.State.Experience = 30000;
                Check(Apply(), "result acknowledgement"); windowType.GetMethod("Update")!.Invoke(scene, [model]);
                Check(labels.Single(l => l.Name == "DiceExperience").Text == "MAX", "max level");
                Check(!(bool)modelType.GetMethod("Apply")!.Invoke(model, [packet, player, 1200L])!, "stale sequence");
                packet.Sequence++; packet.RequestId = 0; packet.State.Experience = 125;
                Check((int)LobbyActivity.RollTheDice == 14 && Enum.IsDefined(LobbyActivity.RollTheDice), "append-only lobby activity");
            }
            finally { windowType.GetMethod("Destroy")!.Invoke(scene, null); }
            Console.WriteLine($"PASS dice UI: {size.X}x{size.Y}, protocol, peers, net return, XP, duplicate guard, lobby");
        }
        foreach (var text in new[] { "Less than 7", "Exactly 7", "More than 7", "Roll the dice", "Players at this table" })
            foreach (var language in new[] { "fr", "es" }) Check(QualityOfLifeText.Get(text, language) != text, "locale " + text);
        Check(QualityOfLifeText.Get("Roll the dice", "unknown") == "Roll the dice", "English fallback");
        foreach (var (source, versions) in QualityOfLifeText.Catalog.Entries)
            if (source.Contains("Two independent dice.") || source.Contains("Stake included") || source.Contains("net {4:"))
                foreach (var language in new[] { "en", "fr", "es" })
                    Check(versions.TryGetValue(language, out var text) && Intersect.Localization.GameTextTokens.Compatible(source, text), "placeholders / " + language);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Dice: " + message); }
}
