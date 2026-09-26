#nullable enable
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames.Cooking;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Blackjack;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Potions;
using Intersect.Server.MiniGames.Roulette;
using Intersect.Server.Networking;
using Intersect.Server.Professions;

namespace Intersect.Server.MiniGames.Cooking;

internal static class CookingRuntime
{
    private sealed class Participant
    {
        public required Player Player;
        public required Client Client;
        public int Actions;
        public int ScoreTotal;
        public int ScoredActions;
    }

    private sealed class Session
    {
        public required Guid Id;
        public required Participant Host;
        public Participant? Partner;
        public CookingRecipeDefinition? Recipe;
        public bool WaitingForPartner;
        public bool PartnerAccepted;
        public bool IngredientsConsumed;
        public bool Complete;
        public int StageIndex = -1;
        public long StageStartedUnixMs;
        public int StageTargetPermille;
        public int StageScoreTotal;
        public int StageScoredActions;
        public readonly Dictionary<Guid, int> StageActions = [];
        public readonly List<int> CompletedStageScores = [];
        public long Revision;
        public long LastHostRequest;
        public long LastPartnerRequest;
        public CookingQuality Quality;
        public int TeamScore;
        public string RewardText = string.Empty;
        public string Status = "Choose a recipe and cook alone or with a party member.";
    }

    private const int PartnerRange = 8;
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Session> SessionsByPlayer = [];
    private static long _sequence;
    private static readonly System.Threading.Timer Timer =
        new(_ => Sweep(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

    internal static bool Contains(Guid playerId)
    {
        lock (Gate) return SessionsByPlayer.ContainsKey(playerId);
    }

    internal static bool Join(Player player)
    {
        _ = Timer;

        if (player.Client is not { IsEditor: false } client)
            return false;

        if (PokerRuntime.HasSeat(player.Id) ||
            BlackjackRuntime.Contains(player.Id) ||
            PotionRuntime.Contains(player.Id) ||
            RouletteRuntime.Contains(player.Id))
        {
            return false;
        }

        CookingStatePacket packet;
        lock (Gate)
        {
            if (SessionsByPlayer.TryGetValue(player.Id, out var existing))
            {
                packet = Project(existing, player, 0);
            }
            else
            {
                var session = new Session
                {
                    Id = Guid.NewGuid(),
                    Host = new Participant { Player = player, Client = client },
                };
                SessionsByPlayer[player.Id] = session;
                packet = Project(session, player, 0);
            }
        }

        SafeSend(client, packet);
        return true;
    }

    internal static bool Leave(Player player)
    {
        Session? session;
        lock (Gate)
        {
            if (!SessionsByPlayer.TryGetValue(player.Id, out session))
                return false;

            RemoveSession(session);
        }

        SendClosed(session, "LeftKitchen");
        return true;
    }

    internal static void Handle(Client client, CookingRequestPacket request)
    {
        if (client.IsEditor || client.Entity is not { } player || !request.IsValid)
            return;

        Session? session;
        lock (Gate)
        {
            if (!SessionsByPlayer.TryGetValue(player.Id, out session) ||
                session.Id != request.SessionId)
            {
                return;
            }

            var participant = GetParticipant(session, player.Id);
            if (participant == null || !ReferenceEquals(participant.Client, client))
                return;

            ref long lastRequest = ref (
                player.Id == session.Host.Player.Id
                    ? ref session.LastHostRequest
                    : ref session.LastPartnerRequest
            );

            if (request.RequestId <= lastRequest)
                return;

            lastRequest = request.RequestId;

            switch (request.Kind)
            {
                case CookingRequestKind.Refresh:
                    SafeSend(client, Project(session, player, request.RequestId));
                    return;

                case CookingRequestKind.Leave:
                    RemoveSession(session);
                    break;

                case CookingRequestKind.StartRecipe:
                    HandleStartRecipe(session, player, request, client);
                    return;

                case CookingRequestKind.RespondInvite:
                    HandleInviteResponse(session, player, request, client);
                    return;

                case CookingRequestKind.Action:
                    HandleAction(session, player, request, client);
                    return;
            }
        }

        SendClosed(session!, "LeftKitchen");
    }

    private static void HandleStartRecipe(
        Session session,
        Player player,
        CookingRequestPacket request,
        Client client
    )
    {
        if (player.Id != session.Host.Player.Id ||
            session.Recipe != null ||
            session.WaitingForPartner ||
            session.Complete)
        {
            SafeSend(client, Project(session, player, request.RequestId, "CannotStartRecipe"));
            return;
        }

        var recipe = RewardConfigurationRuntime.Current.CookingRecipes
            .FirstOrDefault(value => value.IsStructurallyValid && value.Id == request.RecipeId);

        if (recipe == null)
        {
            SafeSend(client, Project(session, player, request.RequestId, "RecipeNotFound"));
            return;
        }

        var hostLevel = ProfessionRuntime.GetLevel(player, recipe.ProfessionId);
        if (!RecipeUnlocked(player, recipe, hostLevel))
        {
            SafeSend(
                client,
                Project(
                    session,
                    player,
                    request.RequestId,
                    recipe.RequiredProfessionLevel > Math.Max(1, hostLevel)
                        ? "ProfessionLevelTooLow"
                        : "RecipeEventLocked"
                )
            );
            return;
        }

        session.Recipe = recipe;

        if (request.PartnerId == Guid.Empty)
        {
            if (!recipe.AllowSolo || recipe.RequireCoop)
            {
                session.Recipe = null;
                SafeSend(client, Project(session, player, request.RequestId, "CoopRequired"));
                return;
            }

            if (!HasIngredients([player], recipe))
            {
                session.Recipe = null;
                SafeSend(client, Project(session, player, request.RequestId, "MissingIngredients"));
                return;
            }

            if (!ConsumeIngredients([player], recipe))
            {
                session.Recipe = null;
                SafeSend(client, Project(session, player, request.RequestId, "InventoryChanged"));
                return;
            }

            session.IngredientsConsumed = true;
            BeginFirstStage(session);
            Broadcast(session, request.RequestId);
            return;
        }

        if (!recipe.AllowCoop)
        {
            session.Recipe = null;
            SafeSend(client, Project(session, player, request.RequestId, "CoopNotAllowed"));
            return;
        }

        var partner = player.Party.FirstOrDefault(member => member.Id == request.PartnerId);
        if (partner == null ||
            partner.Id == player.Id ||
            partner.Client is not { IsEditor: false } partnerClient ||
            partner.MapId != player.MapId ||
            partner.MapInstanceId != player.MapInstanceId ||
            !player.InRangeOf(partner, PartnerRange) ||
            SessionsByPlayer.ContainsKey(partner.Id) ||
            PokerRuntime.HasSeat(partner.Id) ||
            BlackjackRuntime.Contains(partner.Id) ||
            PotionRuntime.Contains(partner.Id) ||
            RouletteRuntime.Contains(partner.Id))
        {
            session.Recipe = null;
            SafeSend(client, Project(session, player, request.RequestId, "PartnerUnavailable"));
            return;
        }

        var partnerLevel = ProfessionRuntime.GetLevel(partner, recipe.ProfessionId);
        if (!RecipeUnlocked(partner, recipe, partnerLevel))
        {
            session.Recipe = null;
            SafeSend(client, Project(session, player, request.RequestId, "PartnerRecipeLocked"));
            return;
        }

        session.Partner = new Participant { Player = partner, Client = partnerClient };
        session.WaitingForPartner = true;
        SessionsByPlayer[partner.Id] = session;
        session.Status = $"{player.Name} invited {partner.Name} to cook {recipe.Name}.";
        ++session.Revision;
        Broadcast(session, request.RequestId);
    }

    private static void HandleInviteResponse(
        Session session,
        Player player,
        CookingRequestPacket request,
        Client client
    )
    {
        if (!session.WaitingForPartner ||
            session.Partner?.Player.Id != player.Id ||
            session.Recipe == null)
        {
            SafeSend(client, Project(session, player, request.RequestId, "NoCookingInvite"));
            return;
        }

        if (!request.Accept)
        {
            var partner = session.Partner;
            if (partner != null)
                SessionsByPlayer.Remove(partner.Player.Id);

            session.Partner = null;
            session.WaitingForPartner = false;
            session.Recipe = null;
            session.Status = "Cooking invitation declined.";
            ++session.Revision;
            SafeSend(client, ClosedPacket(session, player, request.RequestId, "InviteDeclined"));
            SafeSend(session.Host.Client, Project(session, session.Host.Player, 0, "InviteDeclined"));
            return;
        }

        var host = session.Host.Player;
        var partnerPlayer = session.Partner.Player;

        if (!host.Party.Contains(partnerPlayer) ||
            host.MapId != partnerPlayer.MapId ||
            host.MapInstanceId != partnerPlayer.MapInstanceId ||
            !host.InRangeOf(partnerPlayer, PartnerRange))
        {
            SafeSend(client, Project(session, player, request.RequestId, "PartnerUnavailable"));
            return;
        }

        var players = new[] { host, partnerPlayer };
        if (!HasIngredients(players, session.Recipe))
        {
            SafeSend(client, Project(session, player, request.RequestId, "MissingIngredients"));
            SafeSend(session.Host.Client, Project(session, host, 0, "MissingIngredients"));
            return;
        }

        if (!ConsumeIngredients(players, session.Recipe))
        {
            SafeSend(client, Project(session, player, request.RequestId, "InventoryChanged"));
            SafeSend(session.Host.Client, Project(session, host, 0, "InventoryChanged"));
            return;
        }

        session.PartnerAccepted = true;
        session.WaitingForPartner = false;
        session.IngredientsConsumed = true;
        session.Status = $"{host.Name} and {partnerPlayer.Name} are cooking together!";
        BeginFirstStage(session);
        Broadcast(session, request.RequestId);
    }

    private static void HandleAction(
        Session session,
        Player player,
        CookingRequestPacket request,
        Client client
    )
    {
        if (session.Recipe == null ||
            session.StageIndex < 0 ||
            session.StageIndex >= session.Recipe.Stages.Length ||
            session.Complete ||
            session.WaitingForPartner)
        {
            SafeSend(client, Project(session, player, request.RequestId, "NoActiveStage"));
            return;
        }

        var stage = session.Recipe.Stages[session.StageIndex];
        if (!CanAct(session, player.Id, stage))
        {
            SafeSend(client, Project(session, player, request.RequestId, "NotYourStation"));
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var elapsed = now - session.StageStartedUnixMs;
        if (elapsed < 0 || elapsed > stage.DurationSeconds * 1000L)
        {
            FinalizeStage(session);
            Broadcast(session, request.RequestId);
            return;
        }

        var score = ScoreAction(session, stage, elapsed);
        var participant = GetParticipant(session, player.Id)!;
        participant.Actions++;
        participant.ScoreTotal += score;
        participant.ScoredActions++;

        session.StageScoreTotal += score;
        session.StageScoredActions++;
        session.StageActions[player.Id] = session.StageActions.GetValueOrDefault(player.Id) + 1;
        session.Status = CookingStatus(stage.Type, score, player.Name);
        ++session.Revision;

        if (StageComplete(session, stage))
            FinalizeStage(session);

        Broadcast(session, request.RequestId);
    }

    private static bool StageComplete(Session session, CookingStageDefinition stage)
    {
        var total = session.StageActions.Values.Sum();
        if (total < stage.RequiredActions)
            return false;

        if (stage.Assignment == CookingStageAssignment.Both && session.Partner != null)
        {
            return session.StageActions.GetValueOrDefault(session.Host.Player.Id) > 0 &&
                   session.StageActions.GetValueOrDefault(session.Partner.Player.Id) > 0;
        }

        return true;
    }

    private static int ScoreAction(Session session, CookingStageDefinition stage, long elapsedMs)
    {
        var duration = Math.Max(1, stage.DurationSeconds * 1000L);
        var normalized = (double)elapsedMs / duration;
        var cycles = 2d + stage.Difficulty * 0.75d;
        var phase = normalized * cycles;
        var fraction = phase - Math.Floor(phase);
        var cursor = fraction <= 0.5d
            ? (int)Math.Round(fraction * 2000d)
            : (int)Math.Round((1d - fraction) * 2000d);

        var distance = Math.Abs(cursor - session.StageTargetPermille);
        var tolerance = Math.Max(70, 260 - stage.Difficulty * 28);
        if (distance <= tolerance / 4) return 100;
        if (distance <= tolerance / 2) return 90;
        if (distance <= tolerance) return 75;
        if (distance <= tolerance * 2) return 45;
        return 15;
    }

    private static void BeginFirstStage(Session session)
    {
        session.StageIndex = 0;
        session.CompletedStageScores.Clear();
        session.TeamScore = 0;
        session.Quality = CookingQuality.Burnt;
        BeginStage(session);
    }

    private static void BeginStage(Session session)
    {
        if (session.Recipe == null || session.StageIndex >= session.Recipe.Stages.Length)
        {
            CompleteRecipe(session);
            return;
        }

        session.StageStartedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        session.StageTargetPermille = Random.Shared.Next(180, 821);
        session.StageScoreTotal = 0;
        session.StageScoredActions = 0;
        session.StageActions.Clear();
        var stage = session.Recipe.Stages[session.StageIndex];
        session.Status = StagePrompt(stage.Type);
        ++session.Revision;
    }

    private static void FinalizeStage(Session session)
    {
        if (session.Recipe == null || session.Complete)
            return;

        var score = session.StageScoredActions == 0
            ? 0
            : (int)Math.Round(session.StageScoreTotal / (double)session.StageScoredActions);

        session.CompletedStageScores.Add(Math.Clamp(score, 0, 100));
        session.StageIndex++;
        BeginStage(session);
    }

    private static void CompleteRecipe(Session session)
    {
        if (session.Recipe == null || session.Complete)
            return;

        session.Complete = true;
        session.StageIndex = session.Recipe.Stages.Length;
        session.TeamScore = session.CompletedStageScores.Count == 0
            ? 0
            : (int)Math.Round(session.CompletedStageScores.Average());
        session.Quality = CookingRecipeDefinition.QualityForScore(session.TeamScore);

        var output = session.Recipe.OutputFor(session.Quality);
        var participants = Participants(session).ToArray();
        var totalActions = Math.Max(1, participants.Sum(value => value.Actions));
        var rewardParts = new List<string>();

        foreach (var participant in participants)
        {
            var player = participant.Player;

            if (output != null)
            {
                if (player.TryGiveItem(
                        output.ItemId,
                        output.Quantity,
                        ItemHandling.Normal,
                        bankOverflow: true
                    ))
                {
                    rewardParts.Add(
                        $"{player.Name}: {output.Quantity:N0} x {ItemDescriptor.GetName(output.ItemId)}"
                    );
                }
                else
                {
                    PacketSender.SendChatMsg(
                        player,
                        "[Cooking] Your meal could not be delivered. Free inventory/bank space.",
                        ChatMessageType.Error,
                        Color.White
                    );
                }
            }

            var share = participants.Length == 1
                ? 1d
                : participant.Actions / (double)totalActions;
            var effortMultiplier = participants.Length == 1
                ? 1d
                : 0.5d + Math.Clamp(share * participants.Length, 0d, 1.5d) * 0.5d;
            var xp = Math.Max(
                1L,
                (long)Math.Round(
                    session.Recipe.ProfessionExperience *
                    CookingRecipeDefinition.ExperienceMultiplier(session.Quality) *
                    effortMultiplier,
                    MidpointRounding.AwayFromZero
                )
            );

            ProfessionRuntime.AwardActivity(player, session.Recipe.ProfessionId, xp);
        }

        session.RewardText = rewardParts.Count == 0
            ? "Meal finished."
            : string.Join(" | ", rewardParts);
        session.Status =
            $"{session.Quality}! Team score {session.TeamScore}% — " +
            FunnyFinish(session.Quality);
        ++session.Revision;
    }

    private static bool CanAct(Session session, Guid playerId, CookingStageDefinition stage)
    {
        var partnerId = session.Partner?.Player.Id ?? Guid.Empty;

        return stage.Assignment switch
        {
            CookingStageAssignment.Host => playerId == session.Host.Player.Id,
            CookingStageAssignment.Partner => partnerId == Guid.Empty
                ? playerId == session.Host.Player.Id
                : playerId == partnerId,
            CookingStageAssignment.Both => playerId == session.Host.Player.Id || playerId == partnerId,
            _ => partnerId == Guid.Empty
                ? playerId == session.Host.Player.Id
                : session.StageIndex % 2 == 0
                    ? playerId == session.Host.Player.Id
                    : playerId == partnerId,
        };
    }

    private static bool RecipeUnlocked(Player player, CookingRecipeDefinition recipe, int level)
    {
        if (recipe.RequiredProfessionLevel > Math.Max(1, level))
            return false;

        if (recipe.UnlockPlayerVariableId == Guid.Empty)
            return true;

        return player.GetVariableValue(recipe.UnlockPlayerVariableId).Boolean;
    }

    private static bool HasIngredients(IEnumerable<Player> players, CookingRecipeDefinition recipe)
    {
        var group = players.ToArray();
        return recipe.Ingredients.All(
            ingredient =>
                group.Sum(player => (long)player.FindInventoryItemQuantity(ingredient.ItemId)) >=
                ingredient.Quantity
        );
    }

    private static bool ConsumeIngredients(Player[] players, CookingRecipeDefinition recipe)
    {
        var snapshots = players.ToDictionary(
            player => player.Id,
            player => player.Items.Select(item => item.Clone()).ToArray()
        );

        try
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                var remaining = ingredient.Quantity;
                foreach (var player in players)
                {
                    if (remaining <= 0) break;

                    var available = player.FindInventoryItemQuantity(ingredient.ItemId);
                    var take = Math.Min(remaining, available);
                    if (take <= 0) continue;

                    if (!player.TryTakeItem(ingredient.ItemId, take, ItemHandling.Normal, sendUpdate: false))
                        throw new InvalidOperationException("Inventory changed while reserving cooking ingredients.");

                    remaining -= take;
                }

                if (remaining > 0)
                    throw new InvalidOperationException("Cooking ingredients disappeared.");
            }

            foreach (var player in players)
                PacketSender.SendInventory(player);

            return true;
        }
        catch
        {
            foreach (var player in players)
            {
                var snapshot = snapshots[player.Id];
                for (var index = 0; index < Math.Min(player.Items.Count, snapshot.Length); ++index)
                    player.Items[index].Set(snapshot[index]);

                PacketSender.SendInventory(player);
            }

            return false;
        }
    }

    private static CookingStatePacket Project(
        Session session,
        Player viewer,
        long requestId,
        string error = ""
    )
    {
        var recipe = session.Recipe;
        var profession = recipe == null
            ? null
            : ProfessionConfigurationRuntime.Current.Find(recipe.ProfessionId);
        var level = recipe == null ? 0 : ProfessionRuntime.GetLevel(viewer, recipe.ProfessionId);
        var stage = recipe != null &&
                    session.StageIndex >= 0 &&
                    session.StageIndex < recipe.Stages.Length
            ? recipe.Stages[session.StageIndex]
            : null;

        var candidates = viewer.Id == session.Host.Player.Id && recipe == null
            ? viewer.Party
                .Where(member =>
                    member.Id != viewer.Id &&
                    member.Client is { IsEditor: false } &&
                    member.MapId == viewer.MapId &&
                    member.MapInstanceId == viewer.MapInstanceId &&
                    viewer.InRangeOf(member, PartnerRange) &&
                    !SessionsByPlayer.ContainsKey(member.Id))
                .Select(member => new CookingPartyCandidate
                {
                    PlayerId = member.Id,
                    Name = member.Name,
                })
                .ToArray()
            : [];

        var choices = recipe == null
            ? RewardConfigurationRuntime.Current.CookingRecipes
                .Where(value => value.IsStructurallyValid)
                .OrderBy(value => value.RequiredProfessionLevel)
                .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .Select(value =>
                {
                    var viewerLevel = ProfessionRuntime.GetLevel(viewer, value.ProfessionId);
                    var unlocked = RecipeUnlocked(viewer, value, viewerLevel);
                    return new CookingRecipeSummary
                    {
                        Id = value.Id,
                        Name = value.Name,
                        RequiredLevel = value.RequiredProfessionLevel,
                        Experience = value.ProfessionExperience,
                        AllowSolo = value.AllowSolo,
                        AllowCoop = value.AllowCoop,
                        RequireCoop = value.RequireCoop,
                        Unlocked = unlocked,
                        LockedReason = unlocked
                            ? string.Empty
                            : value.RequiredProfessionLevel > Math.Max(1, viewerLevel)
                                ? "Profession level too low"
                                : "Event locked",
                        Ingredients = value.Ingredients.Select(ingredient => new CookingIngredientState
                        {
                            ItemId = ingredient.ItemId,
                            Name = ItemDescriptor.GetName(ingredient.ItemId),
                            Needed = ingredient.Quantity,
                            Available = viewer.FindInventoryItemQuantity(ingredient.ItemId),
                        }).ToArray(),
                    };
                })
                .ToArray()
            : [];

        var elapsed = stage == null
            ? 0L
            : Math.Max(0L, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - session.StageStartedUnixMs);
        var duration = stage?.DurationSeconds * 1000 ?? 0;

        return new CookingStatePacket
        {
            SessionId = session.Id,
            PlayerId = viewer.Id,
            Sequence = Interlocked.Increment(ref _sequence),
            RequestId = requestId,
            ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ErrorCode = error,
            State = new CookingSessionState
            {
                Revision = session.Revision,
                RecipeSelectionRequired = recipe == null,
                WaitingForPartner = session.WaitingForPartner,
                InvitePendingForYou =
                    session.WaitingForPartner && session.Partner?.Player.Id == viewer.Id,
                IsHost = viewer.Id == session.Host.Player.Id,
                HostId = session.Host.Player.Id,
                HostName = session.Host.Player.Name,
                PartnerId = session.Partner?.Player.Id ?? Guid.Empty,
                PartnerName = session.Partner?.Player.Name ?? string.Empty,
                Recipes = choices,
                PartyCandidates = candidates,
                RecipeId = recipe?.Id ?? Guid.Empty,
                RecipeName = recipe?.Name ?? string.Empty,
                ProfessionLevel = level,
                ProfessionName = profession?.Name ?? string.Empty,
                StageIndex = session.StageIndex,
                StageCount = recipe?.Stages.Length ?? 0,
                StageType = stage?.Type ?? CookingStageType.Chop,
                StageAssignment = stage?.Assignment ?? CookingStageAssignment.Auto,
                StageStartedUnixMs = stage == null ? 0 : session.StageStartedUnixMs,
                StageDurationMs = duration,
                TargetPermille = stage == null ? 0 : session.StageTargetPermille,
                TolerancePermille = stage == null ? 0 : Math.Max(70, 260 - stage.Difficulty * 28),
                RequiredActions = stage?.RequiredActions ?? 0,
                CompletedActions = session.StageActions.Values.Sum(),
                YourTurn = stage != null && CanAct(session, viewer.Id, stage),
                StageScore = session.StageScoredActions == 0
                    ? 0
                    : (int)Math.Round(session.StageScoreTotal / (double)session.StageScoredActions),
                TeamScore = session.TeamScore,
                Quality = session.Quality,
                Complete = session.Complete,
                Status = session.Status,
                Participants = Participants(session).Select(value => new CookingParticipantState
                {
                    PlayerId = value.Player.Id,
                    Name = value.Player.Name,
                    Score = value.ScoredActions == 0
                        ? 0
                        : (int)Math.Round(value.ScoreTotal / (double)value.ScoredActions),
                    Actions = value.Actions,
                    Ready = session.IngredientsConsumed,
                }).ToArray(),
                RewardText = session.RewardText,
                StageDifficulty = stage?.Difficulty ?? 0,
            },
        };
    }

    private static void Broadcast(Session session, long requestId = 0, string error = "")
    {
        foreach (var participant in Participants(session))
            SafeSend(participant.Client, Project(session, participant.Player, requestId, error));
    }

    private static void Sweep()
    {
        List<Session> close = [];
        List<Session> update = [];

        lock (Gate)
        {
            foreach (var session in SessionsByPlayer.Values.Distinct().ToArray())
            {
                var participants = Participants(session).ToArray();
                if (participants.Any(value =>
                        value.Player.Client == null ||
                        !ReferenceEquals(value.Player.Client, value.Client) ||
                        !value.Player.IsOnline))
                {
                    close.Add(session);
                    continue;
                }

                if (session.Partner != null &&
                    (!session.Host.Player.Party.Contains(session.Partner.Player) ||
                     session.Host.Player.MapId != session.Partner.Player.MapId ||
                     session.Host.Player.MapInstanceId != session.Partner.Player.MapInstanceId ||
                     !session.Host.Player.InRangeOf(session.Partner.Player, PartnerRange)))
                {
                    close.Add(session);
                    continue;
                }

                if (session.Recipe != null &&
                    !session.Complete &&
                    !session.WaitingForPartner &&
                    session.StageIndex >= 0 &&
                    session.StageIndex < session.Recipe.Stages.Length)
                {
                    var stage = session.Recipe.Stages[session.StageIndex];
                    var elapsed =
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - session.StageStartedUnixMs;
                    if (elapsed >= stage.DurationSeconds * 1000L)
                    {
                        FinalizeStage(session);
                        update.Add(session);
                    }
                }
            }

            foreach (var session in close)
                RemoveSession(session);
        }

        foreach (var session in close)
            SendClosed(session, "KitchenSessionEnded");

        foreach (var session in update.Except(close))
            Broadcast(session);
    }

    private static Participant? GetParticipant(Session session, Guid playerId)
    {
        if (session.Host.Player.Id == playerId) return session.Host;
        if (session.Partner?.Player.Id == playerId) return session.Partner;
        return null;
    }

    private static IEnumerable<Participant> Participants(Session session)
    {
        yield return session.Host;
        if (session.Partner != null) yield return session.Partner;
    }

    private static void RemoveSession(Session session)
    {
        SessionsByPlayer.Remove(session.Host.Player.Id);
        if (session.Partner != null)
            SessionsByPlayer.Remove(session.Partner.Player.Id);
    }

    private static void SendClosed(Session session, string error)
    {
        foreach (var participant in Participants(session))
            SafeSend(participant.Client, ClosedPacket(session, participant.Player, 0, error));
    }

    private static CookingStatePacket ClosedPacket(
        Session session,
        Player player,
        long requestId,
        string error
    ) =>
        new()
        {
            SessionId = session.Id,
            PlayerId = player.Id,
            Sequence = Interlocked.Increment(ref _sequence),
            RequestId = requestId,
            ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Closed = true,
            ErrorCode = error,
        };

    private static void SafeSend(Client? client, CookingStatePacket packet)
    {
        if (client == null) return;
        try { client.Send(packet); }
        catch { }
    }

    private static string StagePrompt(CookingStageType type) => type switch
    {
        CookingStageType.Chop => "CHOP! Hit the sweet spot before the vegetables escape.",
        CookingStageType.Stir => "STIR! Keep the royal sauce moving.",
        CookingStageType.Heat => "HEAT! Do not turn dinner into charcoal.",
        CookingStageType.Flip => "FLIP! Catch it before it meets the floor.",
        CookingStageType.Season => "SEASON! The king asked for flavour, not a salt mine.",
        CookingStageType.Knead => "KNEAD! Show that dough who is in charge.",
        _ => "PLATE! Make it look expensive.",
    };

    private static string CookingStatus(CookingStageType type, int score, string player) =>
        score >= 90
            ? $"{player}: PERFECT {type}! The kitchen applauds."
            : score >= 70
                ? $"{player}: Great {type}! Nobody screamed."
                : score >= 40
                    ? $"{player}: Decent {type}. Still edible."
                    : $"{player}: CHAOS during {type}! Something is smoking.";

    private static string FunnyFinish(CookingQuality quality) => quality switch
    {
        CookingQuality.Perfect => "The plate looks suspiciously professional.",
        CookingQuality.Great => "The tavern customers are fighting for seconds.",
        CookingQuality.Decent => "Nobody asked what happened in the kitchen.",
        _ => "Technically, charcoal is also food-shaped.",
    };
}
