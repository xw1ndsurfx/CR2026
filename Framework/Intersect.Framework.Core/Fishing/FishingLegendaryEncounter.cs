namespace Intersect.Framework.Core.Fishing;

public enum FishingChallengeResult { Ignored, Advanced, Completed, Failed }

/// <summary>Timed legendary interruption. An ignored automatic invitation resumes auto; any accepted encounter stays manual.</summary>
public sealed class FishingLegendaryEncounter
{
    private readonly FishingState _state;
    private readonly int _windowMs;
    private readonly bool _startedAutomatic;
    private bool _accepted;
    private bool _automaticResumeCancelled;
    private long _inputAfter;
    public long Deadline { get; private set; }
    public long ChallengeStarted { get; private set; }
    public bool Completed { get; private set; }
    public bool Expired { get; private set; }
    public bool ResumeAutomaticAfterTimeout { get; private set; }

    public FishingLegendaryEncounter(FishingState state, long now, int promptMs, int steps, int windowMs)
    {
        if (promptMs is < 5000 or > 120000 || steps is < 1 or > 8 || windowMs is < 800 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(promptMs));
        _state = state;
        _windowMs = windowMs;
        _startedAutomatic = state.Automatic;
        state.AutomaticResumePending = state.Automatic;
        Deadline = now + promptMs;
        state.Automatic = state.Reeling = false;
        state.Phase = FishingPhase.LegendaryPrompt;
        state.ChallengeExpectedInput = -1;
        state.ChallengeStep = 0;
        state.ChallengeSteps = steps;
    }

    public bool Accept(long now)
    {
        Advance(now);
        if (_state.Phase != FishingPhase.LegendaryPrompt) return false;
        _accepted = true;
        _state.AutomaticResumePending = false;
        _state.Phase = FishingPhase.Fighting;
        _state.Automatic = false;
        return true;
    }

    public bool CancelAutomaticResume()
    {
        if (_state.Phase != FishingPhase.LegendaryPrompt || !_state.AutomaticResumePending) return false;
        _automaticResumeCancelled = true;
        _state.AutomaticResumePending = false;
        _state.Automatic = false;
        return true;
    }

    public bool BeginChallenge(long now, Func<int> nextInput)
    {
        if (_state.Phase != FishingPhase.Fighting || Completed || Expired) return false;
        _state.Phase = FishingPhase.LegendaryChallenge;
        _state.Reeling = false;
        ChallengeStarted = now;
        Next(now, nextInput);
        return true;
    }

    public FishingChallengeResult Input(int input, long now, Func<int> nextInput)
    {
        Advance(now);
        if (Expired) return FishingChallengeResult.Failed;
        if (_state.Phase != FishingPhase.LegendaryChallenge || now < _inputAfter) return FishingChallengeResult.Ignored;
        if (input != _state.ChallengeExpectedInput)
        {
            Fail();
            return FishingChallengeResult.Failed;
        }
        _state.ChallengeStep++;
        if (_state.ChallengeStep >= _state.ChallengeSteps)
        {
            Completed = true;
            _state.Phase = FishingPhase.Fighting;
            _state.Automatic = false;
            _state.ChallengeExpectedInput = -1;
            return FishingChallengeResult.Completed;
        }
        Next(now, nextInput);
        return FishingChallengeResult.Advanced;
    }

    public void Advance(long now)
    {
        if (_state.Phase is FishingPhase.LegendaryPrompt or FishingPhase.LegendaryChallenge && now >= Deadline)
            Fail(timedOut: true);
    }

    private void Next(long now, Func<int> nextInput)
    {
        var input = nextInput();
        if (input is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(nextInput));
        _state.ChallengeExpectedInput = input;
        _inputAfter = now + 120;
        Deadline = now + _windowMs;
    }

    private void Fail(bool timedOut = false)
    {
        ResumeAutomaticAfterTimeout = timedOut && _state.Phase == FishingPhase.LegendaryPrompt && _startedAutomatic && !_accepted && !_automaticResumeCancelled;
        Expired = true;
        _state.Phase = FishingPhase.Failed;
        _state.Automatic = ResumeAutomaticAfterTimeout;
        _state.AutomaticResumePending = false;
        _state.Reeling = false;
        _state.ChallengeExpectedInput = -1;
    }
}
