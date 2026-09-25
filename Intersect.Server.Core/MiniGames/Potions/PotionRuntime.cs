#nullable enable
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Blackjack;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;
using Intersect.Server.Networking;

namespace Intersect.Server.MiniGames.Potions;

/// <summary>
/// Server-authoritative Royal Alchemy runtime. The client only requests column drops,
/// pair swaps and recipe changes; board state, XP and Intersect item rewards are owned here.
/// </summary>
internal static class PotionRuntime
{
    private sealed class Session
    {
        public required Player Player;
        public required Client Client;
        public required Guid Id;
        public required PotionPuzzle Puzzle;
        public required PotionRecipeDefinition Recipe;
        public required MiniGameProgress Progress;
        public long Revision;
        public long RecipeRound = 1;
        public long LastRequest;
        public bool RewardGranted;
        public string Status = "Choose a column. Three or more touching matches merge upward.";
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Session> Sessions = new();
    private static readonly IMiniGameProgressStore Progress =
        new SqliteMiniGameProgressStore(Path.Combine("resources", "minigames-test.db"));
    private static long _sequence;

    internal static bool Contains(Guid playerId)
    {
        lock (Gate) return Sessions.ContainsKey(playerId);
    }

    internal static bool Join(Player player)
    {
        if (player.Client is not { IsEditor: false } client) return false;
        if (PokerRuntime.HasSeat(player.Id) || BlackjackRuntime.Contains(player.Id)) return false;

        PotionStatePacket? packet;
        lock (Gate)
        {
            if (Sessions.TryGetValue(player.Id, out var existing))
            {
                if (ReferenceEquals(existing.Client, client))
                {
                    packet = Project(existing, 0);
                }
                else
                {
                    Sessions.Remove(player.Id);
                    packet = null;
                }
            }
            else packet = null;

            if (packet == null)
            {
                var progress = Progress.Load(player.Id, MiniGameProgression.Potions);
                var recipe = ChooseRecipe(progress.Level, Guid.Empty);
                if (recipe == null) return false;

                var session = new Session
                {
                    Player = player,
                    Client = client,
                    Id = Guid.NewGuid(),
                    Recipe = recipe,
                    Puzzle = new PotionPuzzle(Random.Shared.Next(1, int.MaxValue), recipe.ToPuzzleRecipe()),
                    Progress = progress,
                };
                Sessions[player.Id] = session;
                packet = Project(session, 0);
            }
        }

        client.Send(packet);
        return true;
    }

    internal static bool Leave(Player player)
    {
        PotionStatePacket? packet = null;
        lock (Gate)
        {
            if (!Sessions.Remove(player.Id, out var session) || !ReferenceEquals(session.Player, player)) return false;
            packet = new PotionStatePacket
            {
                SessionId = session.Id,
                PlayerId = player.Id,
                Sequence = NextSequence(),
                Closed = true,
            };
        }

        sessionSend(packet, player.Client);
        return true;
    }

    internal static void Handle(Client client, PotionRequestPacket request)
    {
        if (client.IsEditor || client.Entity is not { } player || !request.IsValid) return;

        PotionStatePacket? packet;
        var notifyInventory = false;
        lock (Gate)
        {
            if (!Sessions.TryGetValue(player.Id, out var session) ||
                !ReferenceEquals(session.Client, client) ||
                session.Id != request.SessionId)
                return;

            if (request.RequestId <= session.LastRequest)
            {
                packet = Project(session, request.RequestId);
            }
            else if (request.Kind != PotionRequestKind.Refresh && request.Revision != session.Revision)
            {
                session.LastRequest = request.RequestId;
                packet = Project(session, request.RequestId, "StaleState");
            }
            else
            {
                session.LastRequest = request.RequestId;
                var error = string.Empty;

                switch (request.Kind)
                {
                    case PotionRequestKind.Refresh:
                        break;

                    case PotionRequestKind.Drop:
                    {
                        var result = session.Puzzle.Drop(request.Column);
                        if (!result.Success) error = result.Error;
                        else
                        {
                            ++session.Revision;
                            session.Status = result.RecipeCompleted
                                ? "Potion complete. Reward ready."
                                : result.Merges.Length > 0
                                    ? $"Merge chain x{result.Merges.Max(merge => merge.Chain)} • +{result.ScoreGained} score"
                                    : "Pair placed.";
                        }
                        break;
                    }

                    case PotionRequestKind.Swap:
                        session.Puzzle.SwapCurrent();
                        ++session.Revision;
                        session.Status = "Current pair reversed.";
                        break;

                    case PotionRequestKind.Restart:
                        session.Puzzle.RestartBoard();
                        session.RewardGranted = false;
                        ++session.Revision;
                        session.Status = "Board cleared. Recipe progress restarted.";
                        break;

                    case PotionRequestKind.NextRecipe:
                        if (!session.Puzzle.Complete) error = "RecipeNotComplete";
                        else if (!session.RewardGranted) error = "RewardPending";
                        else
                        {
                            var next = ChooseRecipe(session.Progress.Level, session.Recipe.Id);
                            if (next == null) error = "NoUnlockedRecipe";
                            else
                            {
                                session.Recipe = next;
                                session.Puzzle.BeginNextRecipe(next.ToPuzzleRecipe());
                                session.RewardGranted = false;
                                ++session.RecipeRound;
                                ++session.Revision;
                                session.Status = "New recipe selected.";
                            }
                        }
                        break;

                    case PotionRequestKind.Leave:
                        Sessions.Remove(player.Id);
                        packet = new PotionStatePacket
                        {
                            SessionId = session.Id,
                            PlayerId = player.Id,
                            Sequence = NextSequence(),
                            RequestId = request.RequestId,
                            Closed = true,
                        };
                        goto Send;
                }

                if (session.Puzzle.Complete && !session.RewardGranted)
                {
                    if (ItemDescriptor.Get(session.Recipe.OutputItemId) is not { } item)
                    {
                        error = "MissingOutputItem";
                    }
                    else if (!player.TryGiveItem(
                                 session.Recipe.OutputItemId,
                                 session.Recipe.OutputQuantity,
                                 ItemHandling.Normal,
                                 bankOverflow: true))
                    {
                        error = "RewardStorageFull";
                        session.Status = "Reward waiting: free inventory or bank space.";
                    }
                    else
                    {
                        session.Progress = Progress.AwardExperience(
                            player.Id,
                            MiniGameProgression.Potions,
                            session.Id,
                            session.RecipeRound,
                            session.Recipe.CompletionExperience
                        );
                        session.RewardGranted = true;
                        session.Status = $"Brewed {session.Recipe.OutputQuantity:N0} x {item.Name} • +{session.Recipe.CompletionExperience} Alchemy XP";
                        notifyInventory = true;
                    }
                }

                packet = Project(session, request.RequestId, error);
            }
        }

Send:
        if (notifyInventory) PacketSender.SendInventory(player);
        sessionSend(packet, client);
    }

    private static PotionRecipeDefinition? ChooseRecipe(int level, Guid except)
    {
        var unlocked = RewardConfigurationRuntime.Current.PotionRecipes
            .Where(recipe => recipe.IsStructurallyValid && recipe.RequiredLevel <= level && recipe.Id != except)
            .ToArray();

        if (unlocked.Length == 0 && except != Guid.Empty)
        {
            unlocked = RewardConfigurationRuntime.Current.PotionRecipes
                .Where(recipe => recipe.IsStructurallyValid && recipe.RequiredLevel <= level)
                .ToArray();
        }

        return unlocked.Length == 0 ? null : unlocked[Random.Shared.Next(unlocked.Length)];
    }

    private static PotionStatePacket Project(Session session, long requestId, string error = "")
    {
        var board = new int[PotionPuzzle.Columns * PotionPuzzle.Rows];
        for (var row = 0; row < PotionPuzzle.Rows; ++row)
        for (var column = 0; column < PotionPuzzle.Columns; ++column)
            board[row * PotionPuzzle.Columns + column] =
                session.Puzzle.Get(column, row) is { } piece ? PotionStateEncoding.Encode(piece) : 0;

        return new PotionStatePacket
        {
            SessionId = session.Id,
            PlayerId = session.Player.Id,
            Sequence = NextSequence(),
            RequestId = requestId,
            ErrorCode = error,
            State = new PotionSessionState
            {
                Revision = session.Revision,
                RecipeRound = session.RecipeRound,
                Board = board,
                CurrentFirst = PotionStateEncoding.Encode(session.Puzzle.Current.First),
                CurrentSecond = PotionStateEncoding.Encode(session.Puzzle.Current.Second),
                NextFirst = PotionStateEncoding.Encode(session.Puzzle.Next.First),
                NextSecond = PotionStateEncoding.Encode(session.Puzzle.Next.Second),
                RecipeId = session.Recipe.Id,
                RecipeName = session.Recipe.Name,
                RequiredLevel = session.Recipe.RequiredLevel,
                OutputItemName = ItemDescriptor.GetName(session.Recipe.OutputItemId),
                OutputQuantity = session.Recipe.OutputQuantity,
                CompletionExperience = session.Recipe.CompletionExperience,
                Requirements = session.Recipe.Requirements.Select((requirement, index) => new PotionRequirementState
                {
                    Family = (int)requirement.Family,
                    Level = requirement.Level,
                    Needed = requirement.Needed,
                    Progress = session.Puzzle.Progress(index),
                }).ToArray(),
                Score = session.Puzzle.Score,
                Experience = session.Progress.Experience,
                Level = session.Progress.Level,
                RecipesCompleted = session.Progress.Wins,
                Complete = session.Puzzle.Complete,
                GameOver = session.Puzzle.GameOver,
                Status = session.Status,
            },
        };
    }

    private static long NextSequence() => Interlocked.Increment(ref _sequence);

    private static void sessionSend(PotionStatePacket? packet, Client? client)
    {
        if (packet == null || client == null) return;
        try { client.Send(packet); }
        catch { /* Connection teardown owns transport errors. */ }
    }
}
