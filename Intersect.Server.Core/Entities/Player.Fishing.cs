using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities.Events;
using Intersect.Server.Fishing;
using Intersect.Server.Networking;
using Intersect.Server.Professions;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;

public partial class Player
{
    private sealed class FishingSession
    {
        internal required FishingConfiguration Configuration;
        internal required FishingZoneDefinition Zone;
        internal required FishingToolDefinition Tool;
        internal required FishingState State;
        internal Guid StartMapId, InstanceId, AttemptId;
        internal float StartX, StartY;
        internal long LastRequest, LastSent, PhaseStarted, Deadline, FightDeadline, NextAnimation;
        internal readonly long[] SkillReadyAt = new long[4];
        internal FishingCatchDefinition? Catch;
        internal FishingFight? Fight;
        internal FishingLegendaryEncounter? Legendary;
        internal FishingWorldAction WorldAction;
        internal long WorldTransitionAt, WorldRefreshAt;
    }
    private FishingSession? _fishing;
    private long _fishingWorldSequence = DateTime.UtcNow.Ticks;
    internal bool IsFishingActive => _fishing != null;
    internal void LeaveFishingFromEvent() => EndFishing();

    private bool FishingOtherActivity(Event? source = null) => IsDead || HasPendingDeath || IsCasting || IsBusy() ||
        CombatTimer > Timing.Global.Milliseconds || MoveRoute != null || IsArcheryActive || IsJoustProtected ||
        Pvp.PvpRuntime.InMatch(this) || EventLookup.Values.Any(e => e.CinematicActive || e != source && e.HoldingPlayer) ||
        MiniGames.Dice.DiceRuntime.Contains(Id) || MiniGames.Metalworking.MetalworkRuntime.Contains(Id) ||
        MiniGames.Lockpicking.LockpickingRuntime.Contains(Id) || MiniGames.PokerRuntime.HasSeat(Id) ||
        MiniGames.Blackjack.BlackjackRuntime.Contains(Id) || MiniGames.Potions.PotionRuntime.Contains(Id) ||
        MiniGames.Cooking.CookingRuntime.Contains(Id) || MiniGames.Roulette.RouletteRuntime.Contains(Id);

    internal void OpenFishingFromEvent(Guid zoneId, Event source)
    {
        lock (EntityLock)
        {
            if (!InGame || Client is not { IsEditor: false } || !Client.IsConnected() || source?.Descriptor == null || source.CallStack.Count == 0) return;
            if (_fishing != null) { SendFishing(Environment.TickCount64, open: true); return; }
            if (FishingOtherActivity(source)) { FishingError("Finish your current activity before fishing."); return; }
            var config = FishingRuntime.Current;
            var zone = config.Zones.FirstOrDefault(z => z.Id == zoneId && FishingRules.WithinZone(z, MapId, X, Y));
            if (zone == null) { FishingError("Fishing is unavailable here.", "zone_unavailable"); return; }
            var tool = EquippedFishingTool(config);
            if (tool == null) { FishingError("Equip a compatible fishing tool.", "tool_missing", zone); return; }
            var now = Environment.TickCount64;
            var language = Client.LanguageCode ?? "en";
            _fishing = new FishingSession
            {
                Configuration = config, Zone = zone, Tool = tool, StartMapId = MapId, InstanceId = MapInstanceId, StartX = X, StartY = Y,
                PhaseStarted = now,
                State = new FishingState
                {
                    SessionId = Guid.NewGuid(), ZoneId = zone.Id, ZoneName = zone.LocalizedNames.GetValueOrDefault(language)
                        ?? Localization.ContentTranslationRuntime.Translate(zone.Name, language), Phase = FishingPhase.Ready,
                    AllowAutomatic = zone.AllowAutomatic, ToolItemId = tool.ItemId, ToolType = tool.Type,
                    BaitItemId = tool.BaitItemId,
                },
            };
            FishingCue("session_start");
            FishingWorld(FishingWorldAction.Idle, now);
            SendFishing(now, open: true);
        }
    }

    private FishingToolDefinition? EquippedFishingTool(FishingConfiguration config)
    {
        for (var slot = 0; slot < Equipment.Length; slot++)
            if (TryGetEquippedItem(slot, out var equipped) && equipped.Quantity > 0 && config.Tools.FirstOrDefault(t => t.ItemId == equipped.ItemId) is { } tool)
                return tool;
        return null;
    }

    private bool ValidFishing() => _fishing is { } s && InGame && !IsDisposed && Client is { IsEditor: false } && Client.IsConnected() &&
        Client.Entity == this && !IsDead && !HasPendingDeath && MapId == s.StartMapId && MapInstanceId == s.InstanceId && X == s.StartX && Y == s.StartY &&
        FishingRules.WithinZone(s.Zone, MapId, X, Y) && ReferenceEquals(s.Configuration, FishingRuntime.Current) &&
        EquippedFishingTool(s.Configuration)?.ItemId == s.Tool.ItemId && CombatTimer <= Timing.Global.Milliseconds && !IsCasting && !IsBusy();

    internal void HandleFishingRequest(FishingRequestPacket packet)
    {
        lock (EntityLock)
        {
            var s = _fishing;
            if (s == null || packet.SessionId != s.State.SessionId || packet.Sequence <= s.LastRequest || !Enum.IsDefined(packet.Action)) return;
            s.LastRequest = packet.Sequence;
            if (packet.Action == FishingAction.Leave) { EndFishing(); return; }
            if (!ValidFishing()) { EndFishing("Fishing stopped because your position or equipment changed."); return; }
            var now = Environment.TickCount64;
            UpdateFishing(now);
            if (_fishing != s) return;
            switch (packet.Action)
            {
                case FishingAction.Cast:
                    if (s.State.Phase is FishingPhase.Ready or FishingPhase.Caught or FishingPhase.Failed && now >= s.Deadline) CastFishing(now);
                    break;
                case FishingAction.SetMode:
                    if (s.State.Phase == FishingPhase.LegendaryPrompt)
                    {
                        if (!packet.Automatic && s.Legendary?.CancelAutomaticResume() == true)
                        {
                            FishingCue("auto_stop");
                            s.State.Message = "Automatic resumption cancelled. Accept the challenge to fish manually.";
                        }
                        break; // Enabling auto cannot bypass a legendary challenge.
                    }
                    if (s.State.Phase is not (FishingPhase.Ready or FishingPhase.Caught or FishingPhase.Failed))
                    { s.State.Message = "Change fishing mode between catches."; break; }
                    if (packet.Automatic && !s.Zone.AllowAutomatic) break;
                    s.State.Automatic = packet.Automatic;
                    FishingCue(packet.Automatic ? "auto_start" : "auto_stop");
                    s.State.Message = packet.Automatic ? "Automatic fishing started." : "Manual fishing selected.";
                    if (packet.Automatic && now >= s.Deadline) CastFishing(now);
                    break;
                case FishingAction.SetReeling:
                    if (!s.State.Automatic && s.State.Phase == FishingPhase.Fighting)
                    {
                        if (packet.Reeling && !s.State.Reeling) FishingCue("reel");
                        s.State.Reeling = packet.Reeling;
                    }
                    break;
                case FishingAction.AcceptLegendary:
                    if (s.Legendary?.Accept(now) == true) StartFishingFight(now, false);
                    break;
                case FishingAction.ChallengeInput:
                    if (s.State.Phase == FishingPhase.LegendaryChallenge && s.Legendary is { } legendary)
                    {
                        var result = legendary.Input(packet.Input, now, () => Random.Shared.Next(4));
                        if (result == FishingChallengeResult.Failed)
                            FailFishing("The legendary creature escaped. Choose a mode to resume fishing.", now, "legendary_escape", stopAutomatic: true);
                        else if (result == FishingChallengeResult.Completed)
                        {
                            s.FightDeadline += now - legendary.ChallengeStarted;
                            s.Fight?.Pause(now);
                            s.State.Message = "";
                            FishingCue("challenge_success");
                        }
                        else if (result == FishingChallengeResult.Advanced)
                        { s.Deadline = legendary.Deadline; FishingCue("challenge"); }
                    }
                    break;
                case FishingAction.UseSkill:
                    UseFishingSkill(packet.Input, now);
                    break;
            }
            SendFishing(now);
        }
    }

    private FishingCatchDefinition[] EligibleFishing() => _fishing is not { } s ? [] :
        FishingRules.Eligible(s.Zone, s.Tool.ItemId, FishingRewards.Progress(this, s.Configuration).PreviousLevel,
            DateTime.UtcNow.Hour, s.State.Depth, s.State.Automatic, id => GetVariable(id)?.Value.Integer ?? 0);

    private void CastFishing(long now)
    {
        var s = _fishing!;
        if (!ValidFishing()) { EndFishing("Fishing interrupted."); return; }
        if (EligibleFishing().Length == 0)
        { FailFishing("No eligible catch is available with this tool and these conditions.", now, "tool_incompatible", stopAutomatic: true); return; }
        if (!EligibleFishing().Any(c => CanGiveItem(c.ItemId, c.MinimumQuantity)))
        { FailFishing("Your inventory is full. Make room and cast again.", now, s.State.Automatic ? "auto_inventory_full" : "inventory_full", stopAutomatic: true); return; }
        var bait = FishingRewards.ConsumeBait(this, s.Tool.BaitItemId, s.Tool.BaitQuantity);
        if (bait != FishingRewardResult.Success)
        {
            FailFishing(bait == FishingRewardResult.MissingBait ? "Your bait is missing." : "Fishing could not be saved. Please try again.", now,
                bait == FishingRewardResult.MissingBait ? s.State.Automatic ? "auto_bait_empty" : "bait_missing" : "ui_error", stopAutomatic: true);
            return;
        }
        if (s.Tool.BaitItemId != Guid.Empty) RefreshFishingInventory(s.Tool.BaitItemId);
        s.AttemptId = Guid.NewGuid();
        s.Catch = null; s.Fight = null; s.Legendary = null;
        s.State.Phase = FishingPhase.Waiting; s.State.Reeling = false;
        s.State.AutomaticResumePending = false;
        s.State.CatchItemId = s.State.CatchDefinitionId = Guid.Empty;
        s.State.CatchQuantity = s.State.ExperienceGained = s.State.ChallengeStep = 0;
        s.State.ChallengeExpectedInput = -1; s.State.Legendary = false;
        s.State.Progress = 0; s.State.FishStamina = s.State.LineCondition = 100; s.State.Tension = 0;
        s.State.Message = "";
        s.PhaseStarted = now;
        s.Deadline = now + Random.Shared.Next(s.Zone.MinimumWaitMs, s.Zone.MaximumWaitMs + 1);
        s.NextAnimation = now + s.Tool.AnimationIntervalMs;
        FishingAnimation(s.Tool.CastAnimationId);
        FishingWorld(FishingWorldAction.Throw, now);
        FishingCue("cast");
    }

    private void FishingBite(long now)
    {
        var s = _fishing!;
        s.Catch = FishingRules.SelectEncounter(EligibleFishing(), s.Zone.LegendaryChance, Random.Shared.NextDouble(), Random.Shared.NextDouble());
        if (s.Catch == null) { FailFishing("The catch escaped.", now); return; }
        s.State.CatchDefinitionId = s.Catch.Id;
        s.State.CatchItemId = s.Catch.ItemId;
        s.State.CatchQuantity = Random.Shared.Next(s.Catch.MinimumQuantity, s.Catch.MaximumQuantity + 1);
        s.State.Legendary = s.Catch.Legendary;
        if (s.Catch.Legendary)
        {
            s.Legendary = new FishingLegendaryEncounter(s.State, now, s.Zone.LegendaryPromptMs, s.Catch.ChallengeSteps, s.Catch.ChallengeWindowMs);
            s.State.Message = "A legendary creature appeared. Take control before time runs out.";
            s.Deadline = s.Legendary.Deadline;
            s.PhaseStarted = now;
            FishingCue("legendary_hooked");
        }
        else StartFishingFight(now, s.State.Automatic);
    }

    private void StartFishingFight(long now, bool automatic)
    {
        var s = _fishing!;
        s.State.Automatic = automatic;
        s.State.Phase = FishingPhase.Fighting;
        s.State.Reeling = false; s.State.Message = "";
        s.PhaseStarted = now;
        s.FightDeadline = now + (automatic ? s.Zone.AutomaticCatchMs : s.Zone.ActiveLimitMs);
        s.Fight = new FishingFight(now, s.Catch!.Difficulty, s.Tool.Power);
        FishingCue("bite");
        FishingWorld(FishingWorldAction.Fishing, now);
        FishingAnimation(s.Tool.UseAnimationId);
    }

    private void UpdateFishing(long? suppliedNow = null)
    {
        if (_fishing is not { } s) return;
        if (!ValidFishing()) { EndFishing("Fishing stopped because your position or equipment changed."); return; }
        var now = suppliedNow ?? Environment.TickCount64;
        try
        {
            switch (s.State.Phase)
            {
                case FishingPhase.Waiting:
                    if (now >= s.Deadline) FishingBite(now);
                    break;
                case FishingPhase.Fighting:
                    if (s.State.Automatic)
                    {
                        s.State.Progress = Math.Clamp((now - s.PhaseStarted) * 100d / s.Zone.AutomaticCatchMs, 0, 100);
                        s.State.FishStamina = 100 - s.State.Progress;
                        s.State.Tension = 20;
                        if (now >= s.FightDeadline) CompleteFishing(now);
                    }
                    else if (now >= s.FightDeadline) FailFishing("The catch escaped.", now, s.Catch?.Legendary == true ? "legendary_escape" : "escape");
                    else
                    {
                        s.Fight!.Advance(now, s.State.Reeling);
                        s.State.Tension = s.Fight.Tension; s.State.Progress = s.Fight.Progress;
                        s.State.FishStamina = s.Fight.FishStamina; s.State.LineCondition = s.Fight.LineCondition;
                        s.State.Resisting = s.Fight.Resisting;
                        if (s.Fight.Broken) FailFishing("Your line broke.", now, "line_break");
                        else if (s.Catch!.Legendary && s.Legendary is { Completed: false } encounter && s.Fight.Progress >= 55)
                        {
                            encounter.BeginChallenge(now, () => Random.Shared.Next(4));
                            s.Deadline = encounter.Deadline;
                            s.State.Message = "Complete the sequence to subdue this creature.";
                            s.PhaseStarted = now;
                            FishingCue("challenge");
                        }
                        else if (s.Fight.Complete) CompleteFishing(now);
                    }
                    break;
                case FishingPhase.LegendaryPrompt:
                case FishingPhase.LegendaryChallenge:
                    s.Fight?.Pause(now);
                    s.Legendary?.Advance(now);
                    if (s.Legendary is { Expired: true } expired)
                        FailFishing(expired.ResumeAutomaticAfterTimeout
                            ? "The legendary creature escaped. Automatic fishing will resume."
                            : "The legendary creature escaped. Choose a mode to resume fishing.", now, "legendary_timeout",
                            stopAutomatic: !expired.ResumeAutomaticAfterTimeout, resumeAutomatic: expired.ResumeAutomaticAfterTimeout);
                    break;
                case FishingPhase.Ready:
                case FishingPhase.Caught:
                case FishingPhase.Failed:
                    if (FishingRules.ShouldAutoCast(s.State, now, s.Deadline)) CastFishing(now);
                    break;
            }
            if (s.State.Phase is FishingPhase.Waiting or FishingPhase.Fighting && now >= s.NextAnimation)
            { FishingAnimation(s.Tool.UseAnimationId); s.NextAnimation = now + s.Tool.AnimationIntervalMs; }
            if (s.WorldTransitionAt > 0 && now >= s.WorldTransitionAt)
                FishingWorld(s.WorldAction == FishingWorldAction.Throw ? FishingWorldAction.Fishing : FishingWorldAction.Idle, now);
            else if (now >= s.WorldRefreshAt) FishingWorld(s.WorldAction, now, refresh: true);
            if (now - s.LastSent >= 200) SendFishing(now);
        }
        catch (Exception error)
        {
            ApplicationContext.Context.Value?.Logger.LogError(error, "Fishing session failed for {CharacterId}", Id);
            EndFishing("Fishing interrupted.");
        }
    }

    private void UseFishingSkill(int index, long now)
    {
        var s = _fishing!;
        if (index is < 0 or > 3 || s.State.Automatic || now < s.SkillReadyAt[index]) return;
        var skill = s.Configuration.Skills.FirstOrDefault(p => (int)p.Skill == index && p.Enabled);
        if (skill == null || FishingRewards.Progress(this, s.Configuration).PreviousLevel < skill.MinimumLevel) return;
        if (skill.Skill == FishingSkillType.Dive)
        {
            if (s.State.Phase != FishingPhase.Waiting || s.State.Depth >= 100) return;
            s.State.Depth = Math.Clamp(s.State.Depth + skill.Amount, 0, 100);
        }
        else if (s.State.Phase != FishingPhase.Fighting || s.Fight?.UseSkill(skill, now) != true) return;
        s.SkillReadyAt[index] = now + skill.CooldownMs;
        FishingCue(new[] { "skill_stabilize", "skill_pull", "skill_repair", "skill_sink" }[index]);
        // Apply skill effects immediately; the server still decides failure, challenges and completion.
        UpdateFishing(now);
    }

    private void CompleteFishing(long now)
    {
        var s = _fishing!;
        if (!ValidFishing() || s.Catch == null || s.Catch.Legendary && s.Legendary?.Completed != true) return;
        if (!EligibleFishing().Any(c => c.Id == s.Catch.Id))
        { FailFishing("No eligible catch is available with this tool and these conditions.", now, "tool_incompatible", stopAutomatic: true); return; }
        var award = FishingRewards.Progress(this, s.Configuration, s.Catch.Experience);
        var result = FishingRewards.Reward(this, s.AttemptId, s.Catch.ItemId, s.State.CatchQuantity, award);
        if (result is not (FishingRewardResult.Success or FishingRewardResult.AlreadyCommitted))
        {
            FailFishing(result == FishingRewardResult.InventoryFull ? "Your inventory is full. Make room and cast again." :
                "Fishing could not be saved. Please try again.", now, result == FishingRewardResult.InventoryFull ?
                    s.State.Automatic ? "auto_inventory_full" : "inventory_full" : "ui_error", stopAutomatic: true);
            return;
        }
        s.State.Phase = FishingPhase.Caught; s.State.Reeling = false; s.State.Progress = 100; s.State.FishStamina = 0;
        s.State.CatchesThisSession += s.State.CatchQuantity; s.State.ExperienceGained = (int)Math.Min(int.MaxValue, award.Gained);
        s.State.Message = "Catch secured."; s.Deadline = now + 1500;
        FishingCue(s.Catch.Legendary ? "legendary_catch" : "reward_item");
        FishingAnimation(s.Tool.CatchAnimationId);
        FishingWorld(FishingWorldAction.Catch, now);
        if (result == FishingRewardResult.Success)
        {
            RefreshFishingInventory(s.Catch.ItemId);
            SendPacket(new ItemChangeNotificationPacket(s.Catch.ItemId, s.State.CatchQuantity));
            if (award.Profession != null) ProfessionRuntime.PublishFishingAward(this, award.Profession, award.PreviousLevel, award.Gained);
        }
        SendFishing(now);
    }

    private void FailFishing(string message, long now, string cue = "escape", bool stopAutomatic = false, bool resumeAutomatic = false)
    {
        if (_fishing is not { } s) return;
        s.State.Phase = FishingPhase.Failed; s.State.Reeling = false;
        s.State.Message = message; s.Deadline = now + 1500;
        if (resumeAutomatic) s.State.Automatic = true;
        else if (stopAutomatic || s.Catch?.Legendary == true) s.State.Automatic = false;
        FishingCue(cue);
        FishingWorld(FishingWorldAction.Idle, now);
    }

    private void FishingCue(string key)
    {
        if (_fishing is not { } s) return;
        s.State.SoundCue = key;
        s.State.Sound = FishingSoundCatalog.Resolve(s.Configuration, s.Zone, s.Tool, s.Catch, key).File;
        s.State.SoundSequence++;
    }

    private void FishingAnimation(Guid id)
    {
        if (id != Guid.Empty && AnimationDescriptor.Get(id) != null)
            PacketSender.SendAnimationToProximity(id, 1, Id, MapId, 0, 0, Dir, MapInstanceId);
    }

    private void FishingWorld(FishingWorldAction action, long now, bool refresh = false)
    {
        if (_fishing is not { } s) return;
        s.WorldAction = action;
        if (!refresh)
        {
            s.WorldTransitionAt = 0;
            if (action is FishingWorldAction.Throw or FishingWorldAction.Catch && s.Tool.WorldProfile is { Enabled: true } profile &&
                profile.Clips.TryGetValue(action, out var clip))
                s.WorldTransitionAt = now + (int)Math.Ceiling(clip.Frames * 1000d / profile.FramesPerSecond);
        }
        s.WorldRefreshAt = now + 3000;
        if (s.Tool.WorldProfile?.Enabled != true && action != FishingWorldAction.Stop) return;
        PacketSender.SendDataToProximityOnMapInstance(s.StartMapId, s.InstanceId, new FishingWorldPacket
        {
            EntityId = Id, ToolItemId = s.Tool.ItemId, Action = action, Sequence = ++_fishingWorldSequence,
            ProfileJson = action == FishingWorldAction.Stop ? "" : System.Text.Json.JsonSerializer.Serialize(s.Tool.WorldProfile, FishingConfiguration.JsonOptions),
        });
    }

    private void RefreshFishingInventory(Guid itemId)
    {
        PacketSender.SendInventory(this);
        UpdateGatherItemQuests(itemId);
    }

    private void SendFishing(long now, bool open = false)
    {
        if (_fishing is not { } s) return;
        var state = s.State;
        var progress = FishingRewards.Progress(this, s.Configuration);
        state.Level = progress.PreviousLevel;
        state.Experience = progress.Before;
        state.MaximumLevel = progress.Profession?.MaximumLevel ?? MiniGameProgression.MaximumLevel;
        var floor = progress.Profession?.ExperienceToReachLevel(state.Level) ?? MiniGameProgression.ExperienceAtLevel(state.Level);
        state.ExperienceIntoLevel = Math.Max(0, state.Experience - floor);
        state.ExperienceRequired = state.Level >= state.MaximumLevel ? 0 :
            (progress.Profession?.ExperienceToReachLevel(state.Level + 1) ?? MiniGameProgression.ExperienceAtLevel(state.Level + 1)) - floor;
        state.RemainingMs = (int)Math.Clamp((state.Phase == FishingPhase.Fighting ? s.FightDeadline : s.Deadline) - now, 0, int.MaxValue);
        state.RemainingBait = s.Tool.BaitItemId == Guid.Empty ? 0 : (int)Math.Min(int.MaxValue,
            Items.Where(item => item.ItemId == s.Tool.BaitItemId).Sum(item => (long)Math.Max(0, item.Quantity)));
        for (var index = 0; index < 4; index++)
        {
            state.SkillCooldownsMs[index] = (int)Math.Clamp(s.SkillReadyAt[index] - now, 0, int.MaxValue);
            var skill = s.Configuration.Skills.FirstOrDefault(p => (int)p.Skill == index);
            state.SkillAvailable[index] = skill is { Enabled: true } && state.Level >= skill.MinimumLevel && !state.Automatic &&
                state.SkillCooldownsMs[index] == 0 && (index == 3 ? state.Phase == FishingPhase.Waiting && state.Depth < 100 :
                    state.Phase == FishingPhase.Fighting && (index != 2 || state.LineCondition < 100));
        }
        var catchIds = s.Configuration.Zones.SelectMany(z => z.Catches).Select(c => c.ItemId).ToHashSet();
        state.Inventory = Items.Where(item => catchIds.Contains(item.ItemId) && item.Quantity > 0).GroupBy(item => item.ItemId)
            .Select(group => new FishingInventoryEntry { ItemId = group.Key, Quantity = (int)Math.Min(int.MaxValue, group.Sum(i => (long)i.Quantity)) }).ToList();
        state.Revision++; s.LastSent = now;
        var originalMessage = state.Message;
        state.Message = FishingText.Get(originalMessage, Client?.LanguageCode ?? "en");
        try { SendPacket(new FishingStatePacket { Open = open, StateJson = state.ToJson(), ConfigurationJson = open ? s.Configuration.ToJson() : "" }); }
        finally { state.Message = originalMessage; }
    }

    private void FishingError(string message, string cue = "ui_error", FishingZoneDefinition? zone = null) => SendPacket(new FishingStatePacket
    {
        Message = FishingText.Get(message, Client?.LanguageCode ?? "en"),
        NoticeSoundJson = System.Text.Json.JsonSerializer.Serialize(FishingSoundCatalog.Resolve(FishingRuntime.Current, zone, null, null, cue), FishingConfiguration.JsonOptions),
    });

    private void EndFishing(string message = "", string? soundCue = null)
    {
        var entityLock = EntityLock;
        // Constructor-free players can reach lifecycle cleanup without a lock or session.
        // Initialized players must still acquire the gate, even when idle, to serialize
        // cleanup with a concurrent session start. An active session always requires it.
        if (entityLock is null && _fishing is null) return;
        ArgumentNullException.ThrowIfNull(entityLock, nameof(EntityLock));
        lock (entityLock)
        {
            if (_fishing is not { } s) return;
            s.State.Phase = FishingPhase.Closed; s.State.Automatic = s.State.Reeling = false;
            s.State.AutomaticResumePending = false;
            s.State.Message = message;
            FishingCue(soundCue ?? (string.IsNullOrEmpty(message) ? "session_end" : "session_interrupted"));
            FishingWorld(FishingWorldAction.Stop, Environment.TickCount64);
            try { SendFishing(Environment.TickCount64); }
            finally { _fishing = null; }
        }
    }

    private void ValidateFishingEquipment()
    {
        if (_fishing is { } s && EquippedFishingTool(s.Configuration)?.ItemId != s.Tool.ItemId)
            EndFishing("Fishing stopped because your position or equipment changed.", "tool_changed");
    }
}
