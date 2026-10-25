namespace Intersect.Framework.Core.Jousting;

public readonly record struct JoustAim(double X, double Y, bool Defending);
public readonly record struct JoustStrike(JoustHit Hit, int Points, bool BrokenLance);

/// <summary>Server-owned monotonic simulation. No client timestamp, result, power or premium stat is accepted.</summary>
public sealed class JoustMatch
{
    private readonly JoustRules _rules;
    private readonly Random _random;
    private long _now;
    private long _aiAt;
    private long _aiNextDecision;
    private readonly long[] _inputAt;
    private readonly long[] _sequence = [-1, -1];
    private readonly long[] _stableSince;
    private readonly bool[] _acted = [false, false];
    private readonly JoustAim[] _stableAnchor = [new(.5, .5, false), new(.5, .5, false)];
    private readonly JoustAim[] _aim = [new(.5, .5, false), new(.5, .5, false)];
    private JoustAim _aiTarget = new(.5, .5, false);
    public Guid Id { get; } = Guid.NewGuid();
    public JoustDifficulty Difficulty { get; }
    public JoustPhase Phase { get; private set; } = JoustPhase.Preparation;
    public int Pass { get; private set; } = 1;
    public long PhaseStarted { get; private set; }
    public long Deadline { get; private set; }
    public long ChargeStarted { get; private set; }
    public long ImpactStarted { get; private set; }
    public int ResolvedPass { get; private set; }
    public int[] Scores { get; } = [0, 0];
    public JoustStrike[] Strikes { get; } = [new(JoustHit.Miss, 0, false), new(JoustHit.Miss, 0, false)];
    public int Winner { get; private set; } = -1;
    public bool IsPve { get; }
    public bool Done => Phase is JoustPhase.Finished or JoustPhase.Abandoned;
    public JoustAim Aim(int side) => _aim[side];
    public int Passes => _rules.Passes;
    // Only the server may hold preparation while a native introduction owns the player.
    public void HoldPreparation(long now)
    {
        if (Phase != JoustPhase.Preparation || now < _now) return;
        Deadline += now - _now; PhaseStarted += now - _now; _now = now;
    }

    public JoustMatch(JoustRules rules, long now, JoustDifficulty difficulty, bool pve = true, int? seed = null)
    {
        if (!rules.IsValid || !Enum.IsDefined(difficulty) || now < 0) throw new ArgumentException("joust-match");
        // Freeze the mutable editor configuration for the lifetime of this match.
        _rules = System.Text.Json.JsonSerializer.Deserialize<JoustRules>(System.Text.Json.JsonSerializer.Serialize(rules))!;
        _now = PhaseStarted = now; Deadline = now + _rules.PreparationMs;
        Difficulty = difficulty; IsPve = pve; _random = seed.HasValue ? new(seed.Value) : new();
        _inputAt = [now, now]; _stableSince = [now, now]; _aiAt = now;
    }

    public bool Submit(int side, int pass, long sequence, JoustAim aim, long now)
    {
        if (now < _now) return false;
        Advance(now);
        if (Done || Phase != JoustPhase.Aim || side is < 0 or > 1 || IsPve && side == 1 || pass != Pass ||
            sequence <= _sequence[side] || !double.IsFinite(aim.X) || !double.IsFinite(aim.Y) ||
            aim.X is < 0 or > 1 || aim.Y is < 0 or > 1 || now - _inputAt[side] < 40) return false;
        _sequence[side] = sequence;
        SetAim(side, aim, now, _rules.AimSpeed);
        return true;
    }

    private void SetAim(int side, JoustAim desired, long now, double speed)
    {
        var previous = _aim[side];
        // Cap accumulated movement credit: a paused client cannot teleport its lance.
        var distance = Math.Sqrt(Math.Pow(desired.X - previous.X, 2) + Math.Pow(desired.Y - previous.Y, 2));
        var limit = Math.Clamp(now - _inputAt[side], 0, 200) / 1000d * speed;
        var ratio = distance <= limit || distance == 0 ? 1 : limit / distance;
        var next = new JoustAim(previous.X + (desired.X - previous.X) * ratio,
            previous.Y + (desired.Y - previous.Y) * ratio, desired.Defending);
        var anchor = _stableAnchor[side];
        if (Math.Sqrt(Math.Pow(next.X - anchor.X, 2) + Math.Pow(next.Y - anchor.Y, 2)) > .012 || previous.Defending != desired.Defending)
        { _stableSince[side] = now; _stableAnchor[side] = next; }
        _aim[side] = next; _inputAt[side] = now;
        _acted[side] = true;
    }

    public void Advance(long now)
    {
        if (now < _now) return;
        // Resolve every boundary using its original deadline, even after a delayed server tick.
        while (!Done && now >= Deadline)
        {
            if (Phase == JoustPhase.Aim) UpdateAi(Deadline);
            var boundary = Deadline;
            PhaseStarted = boundary;
            switch (Phase)
            {
                case JoustPhase.Preparation: Phase = JoustPhase.Charge; ChargeStarted = boundary; Deadline += _rules.ChargeMs; break;
                case JoustPhase.Charge:
                    Phase = JoustPhase.Aim; Deadline += _rules.AimMs;
                    _aiAt = _aiNextDecision = boundary;
                    for (var i = 0; i < 2; i++) { _inputAt[i] = _stableSince[i] = boundary; _aim[i] = _stableAnchor[i] = new(.5, .75, false); _acted[i] = false; }
                    break;
                case JoustPhase.Aim:
                    // Capture both attacks before either result affects the duel.
                    var a = Resolve(0, boundary); var b = Resolve(1, boundary);
                    Strikes[0] = a; Strikes[1] = b; Scores[0] += a.Points; Scores[1] += b.Points;
                    ResolvedPass = Pass; ImpactStarted = boundary;
                    Phase = JoustPhase.Impact; Deadline += _rules.ImpactMs; break;
                case JoustPhase.Impact: Phase = JoustPhase.Results; Deadline += _rules.ResultsMs; break;
                case JoustPhase.Results:
                    var down0 = Strikes[1].Hit == JoustHit.Unhorsed; var down1 = Strikes[0].Hit == JoustHit.Unhorsed;
                    if (Pass >= _rules.Passes || _rules.UnhorseEndsMatch && (down0 || down1))
                    {
                        Winner = down0 != down1 && _rules.UnhorseEndsMatch ? (down0 ? 1 : 0) :
                            Scores[0] == Scores[1] ? -1 : Scores[0] > Scores[1] ? 0 : 1;
                        Phase = JoustPhase.Finished;
                    }
                    else { Pass++; Phase = JoustPhase.Preparation; Deadline += _rules.PreparationMs; }
                    break;
            }
        }
        if (Phase == JoustPhase.Aim) UpdateAi(now);
        _now = now;
    }

    private void UpdateAi(long until)
    {
        if (!IsPve) return;
        var level = (int)Difficulty;
        // Fixed 50 ms steps make the AI independent of request frequency and server tick grouping.
        while (_aiAt + 50 <= until)
        {
            _aiAt += 50;
            if (_aiAt >= _aiNextDecision)
            {
                var spread = .30 - level * .055;
                _aiTarget = new(Math.Clamp((_rules.BodyTarget.Left + _rules.BodyTarget.Right) / 2 + (_random.NextDouble() * 2 - 1) * spread, 0, 1),
                    Math.Clamp((_rules.BodyTarget.Top + _rules.BodyTarget.Bottom) / 2 + (_random.NextDouble() * 2 - 1) * spread, 0, 1),
                    _random.NextDouble() < .12 + level * .14);
                _aiNextDecision = _aiAt + 650 - level * 100;
            }
            SetAim(1, _aiTarget, _aiAt, .35 + level * .2);
        }
    }

    private JoustStrike Resolve(int side, long at)
    {
        var attack = _aim[side]; var defense = _aim[1 - side].Defending;
        if (!_acted[side]) return new(JoustHit.Miss, 0, false);
        // Normalized silhouette: head/horse/outside are never rewarded.
        var onBody = _rules.BodyTarget.Contains(attack.X, attack.Y);
        var onShield = _rules.ShieldTarget.Contains(attack.X, attack.Y);
        if (!onBody && !onShield) return new(JoustHit.Miss, 0, false);
        var stable = at - _stableSince[side] >= 300;
        var bonus = stable ? _rules.BrokenLancePoints : 0;
        if (onShield || defense) return new(JoustHit.Shield, _rules.ShieldPoints + bonus, stable);
        var precise = _rules.PreciseTarget.Contains(attack.X, attack.Y) && stable;
        var unhorse = precise && !attack.Defending && at - _stableSince[side] >= 700;
        return new(unhorse ? JoustHit.Unhorsed : precise ? JoustHit.Precise : JoustHit.Body,
            (precise ? _rules.PrecisePoints : _rules.BodyPoints) + bonus, stable);
    }

    public void Abandon(int side)
    {
        if (Done || side is < 0 or > 1) return;
        Winner = 1 - side; Phase = JoustPhase.Abandoned;
    }
}
