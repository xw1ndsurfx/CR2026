using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.MiniGames;
using Intersect.Framework.Core.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Intersect.Tests.GameObjects.MiniGames;

[TestFixture]
public sealed class StartMiniGameCommandTests
{
    [TestCase(EventCommandType.Null, 0)]
    [TestCase(EventCommandType.ShowText, 1)]
    [TestCase(EventCommandType.ShowOptions, 2)]
    [TestCase(EventCommandType.AddChatboxText, 3)]
    [TestCase(EventCommandType.SetVariable, 5)]
    [TestCase(EventCommandType.SetSelfSwitch, 6)]
    [TestCase(EventCommandType.ConditionalBranch, 7)]
    [TestCase(EventCommandType.ExitEventProcess, 8)]
    [TestCase(EventCommandType.Label, 9)]
    [TestCase(EventCommandType.GoToLabel, 10)]
    [TestCase(EventCommandType.StartCommonEvent, 11)]
    [TestCase(EventCommandType.RestoreHp, 12)]
    [TestCase(EventCommandType.RestoreMp, 13)]
    [TestCase(EventCommandType.LevelUp, 14)]
    [TestCase(EventCommandType.GiveExperience, 15)]
    [TestCase(EventCommandType.ChangeLevel, 16)]
    [TestCase(EventCommandType.ChangeSpells, 17)]
    [TestCase(EventCommandType.ChangeItems, 18)]
    [TestCase(EventCommandType.ChangeSprite, 19)]
    [TestCase(EventCommandType.ChangeFace, 20)]
    [TestCase(EventCommandType.ChangeGender, 21)]
    [TestCase(EventCommandType.SetAccess, 22)]
    [TestCase(EventCommandType.WarpPlayer, 23)]
    [TestCase(EventCommandType.SetMoveRoute, 24)]
    [TestCase(EventCommandType.WaitForRouteCompletion, 25)]
    [TestCase(EventCommandType.HoldPlayer, 26)]
    [TestCase(EventCommandType.ReleasePlayer, 27)]
    [TestCase(EventCommandType.SpawnNpc, 28)]
    [TestCase(EventCommandType.PlayAnimation, 29)]
    [TestCase(EventCommandType.PlayBgm, 30)]
    [TestCase(EventCommandType.FadeoutBgm, 31)]
    [TestCase(EventCommandType.PlaySound, 32)]
    [TestCase(EventCommandType.StopSounds, 33)]
    [TestCase(EventCommandType.Wait, 34)]
    [TestCase(EventCommandType.OpenBank, 35)]
    [TestCase(EventCommandType.OpenShop, 36)]
    [TestCase(EventCommandType.OpenCraftingTable, 37)]
    [TestCase(EventCommandType.SetClass, 38)]
    [TestCase(EventCommandType.DespawnNpc, 39)]
    [TestCase(EventCommandType.StartQuest, 40)]
    [TestCase(EventCommandType.CompleteQuestTask, 41)]
    [TestCase(EventCommandType.EndQuest, 42)]
    [TestCase(EventCommandType.ShowPicture, 43)]
    [TestCase(EventCommandType.HidePicture, 44)]
    [TestCase(EventCommandType.HidePlayer, 45)]
    [TestCase(EventCommandType.ShowPlayer, 46)]
    [TestCase(EventCommandType.EquipItem, 47)]
    [TestCase(EventCommandType.ChangeNameColor, 48)]
    [TestCase(EventCommandType.InputVariable, 49)]
    [TestCase(EventCommandType.PlayerLabel, 50)]
    [TestCase(EventCommandType.ChangePlayerColor, 51)]
    [TestCase(EventCommandType.ChangeName, 52)]
    [TestCase(EventCommandType.CreateGuild, 53)]
    [TestCase(EventCommandType.DisbandGuild, 54)]
    [TestCase(EventCommandType.OpenGuildBank, 55)]
    [TestCase(EventCommandType.SetGuildBankSlots, 56)]
    [TestCase(EventCommandType.ResetStatPointAllocations, 57)]
    [TestCase(EventCommandType.CastSpellOn, 58)]
    [TestCase(EventCommandType.Fade, 59)]
    public void ExistingCommandValuesRemainStable(EventCommandType type, int expected)
    {
        Assert.That((int)type, Is.EqualTo(expected));
    }

    [Test]
    public void NewCommandIsAppendedAfterExistingCommands()
    {
        Assert.That((int)EventCommandType.StartMiniGame, Is.EqualTo((int)EventCommandType.Fade + 1));
        Assert.That(new StartMiniGameCommand().Type, Is.EqualTo(EventCommandType.StartMiniGame));
    }

    [Test]
    public void DefaultPokerOptionsAreValid()
    {
        var command = CreateCommand();
        Assert.That(command.GetValidationErrors(), Is.Empty);
        AssertDefaults(command);
    }

    [Test]
    public void MissingTableIdIsRejected()
    {
        Assert.That(new StartMiniGameCommand().GetValidationErrors(), Is.Not.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(999)]
    public void UnsupportedGameIsRejected(int game)
    {
        var command = CreateCommand();
        command.Game = (MiniGameType)game;
        Assert.That(command.GetValidationErrors(), Is.Not.Empty);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    public void InvalidSeatCountsAreRejected(int seats)
    {
        var command = CreateCommand();
        command.Poker.MaxPlayers = seats;
        Assert.That(command.GetValidationErrors(), Is.Not.Empty);
    }

    [TestCase(-1L)]
    [TestCase(0L)]
    [TestCase(1_000_001L)]
    [TestCase(long.MaxValue)]
    public void InvalidStartingChipsAreRejected(long chips)
    {
        var command = CreateCommand();
        command.Poker.StartingChips = chips;
        Assert.That(command.GetValidationErrors(), Is.Not.Empty);
    }

    [TestCase(-1L)]
    [TestCase(0L)]
    [TestCase(20L)]
    [TestCase(long.MaxValue)]
    public void InvalidSmallBlindsAreRejected(long blind)
    {
        var command = CreateCommand();
        command.Poker.SmallBlind = blind;
        Assert.That(command.GetValidationErrors(), Is.Not.Empty);
    }

    [TestCase(-1L)]
    [TestCase(0L)]
    [TestCase(10L)]
    [TestCase(1001L)]
    [TestCase(long.MaxValue)]
    public void InvalidBigBlindsAreRejected(long blind)
    {
        var command = CreateCommand();
        command.Poker.BigBlind = blind;
        Assert.That(command.GetValidationErrors(), Is.Not.Empty);
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(301)]
    public void InvalidTurnTimeoutsAreRejected(int seconds)
    {
        var command = CreateCommand();
        command.Poker.TurnTimeoutSeconds = seconds;
        Assert.That(command.GetValidationErrors(), Is.Not.Empty);
    }

    [TestCase(2, 5, 20L)]
    [TestCase(6, 300, 1_000_000L)]
    public void ValidBoundaryOptionsAreAccepted(int seats, int seconds, long chips)
    {
        var command = CreateCommand();
        command.Poker.MaxPlayers = seats;
        command.Poker.TurnTimeoutSeconds = seconds;
        command.Poker.StartingChips = chips;
        Assert.That(command.GetValidationErrors(), Is.Empty);
    }

    [Test]
    public void DefaultValuesSurviveEventCopySerialization()
    {
        var original = CreateCommand();
        var copy = DeserializeCopy(SerializeCopy(original));
        Assert.That(copy.TableId, Is.EqualTo(original.TableId));
        AssertDefaults(copy);
        Assert.That(copy.GetValidationErrors(), Is.Empty);
    }

    [Test]
    public void CustomOptionsSurviveEventCopySerialization()
    {
        var original = CreateCommand();
        original.Poker.MaxPlayers = 4;
        original.Poker.StartingChips = 5000;
        original.Poker.SmallBlind = 25;
        original.Poker.BigBlind = 50;
        original.Poker.TurnTimeoutSeconds = 45;

        var copy = DeserializeCopy(SerializeCopy(original));
        Assert.That(copy.TableId, Is.EqualTo(original.TableId));
        Assert.That(copy.Poker, Is.Not.SameAs(original.Poker));
        Assert.That(copy.Poker.MaxPlayers, Is.EqualTo(4));
        Assert.That(copy.Poker.StartingChips, Is.EqualTo(5000L));
        Assert.That(copy.Poker.SmallBlind, Is.EqualTo(25L));
        Assert.That(copy.Poker.BigBlind, Is.EqualTo(50L));
        Assert.That(copy.Poker.TurnTimeoutSeconds, Is.EqualTo(45));
        Assert.That(copy.GetValidationErrors(), Is.Empty);
    }

    [Test]
    public void OmittedPokerOptionsKeepTheirDefaults()
    {
        var json = JObject.Parse(SerializeCopy(CreateCommand()));
        json.Remove(nameof(StartMiniGameCommand.Poker));
        var copy = DeserializeCopy(json.ToString());
        AssertDefaults(copy);
        Assert.That(copy.GetValidationErrors(), Is.Empty);
    }

    [Test]
    public void ExplicitNullPokerOptionsAreRejected()
    {
        var json = JObject.Parse(SerializeCopy(CreateCommand()));
        json[nameof(StartMiniGameCommand.Poker)] = JValue.CreateNull();
        var copy = DeserializeCopy(json.ToString());
        Assert.That(copy.GetValidationErrors(), Is.Not.Empty);
    }

    [Test]
    public void ExistingShopCommandStillRoundTrips()
    {
        var original = new OpenShopCommand { ShopId = Guid.NewGuid() };
        var copy = JsonConvert.DeserializeObject<EventCommand>(SerializeCopy(original), CopySettings());
        Assert.That(copy, Is.TypeOf<OpenShopCommand>());
        Assert.That(((OpenShopCommand)copy!).ShopId, Is.EqualTo(original.ShopId));
    }

    private static StartMiniGameCommand CreateCommand() => new() { TableId = Guid.NewGuid() };

    private static string SerializeCopy(EventCommand command) => command.GetCopyData(
        new Dictionary<Guid, List<EventCommand>>(),
        new Dictionary<Guid, List<EventCommand>>()
    );

    private static JsonSerializerSettings CopySettings() => new()
    {
        SerializationBinder = new IntersectTypeSerializationBinder(),
        TypeNameHandling = TypeNameHandling.Auto,
        DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private static StartMiniGameCommand DeserializeCopy(string json)
    {
        var command = JsonConvert.DeserializeObject<EventCommand>(json, CopySettings());
        Assert.That(command, Is.TypeOf<StartMiniGameCommand>());
        return (StartMiniGameCommand)command!;
    }

    private static void AssertDefaults(StartMiniGameCommand command)
    {
        Assert.That(command.Game, Is.EqualTo(MiniGameType.Poker));
        Assert.That(command.Poker, Is.Not.Null);
        Assert.That(command.Poker.MaxPlayers, Is.EqualTo(6));
        Assert.That(command.Poker.StartingChips, Is.EqualTo(1000L));
        Assert.That(command.Poker.SmallBlind, Is.EqualTo(10L));
        Assert.That(command.Poker.BigBlind, Is.EqualTo(20L));
        Assert.That(command.Poker.TurnTimeoutSeconds, Is.EqualTo(30));
    }
}
