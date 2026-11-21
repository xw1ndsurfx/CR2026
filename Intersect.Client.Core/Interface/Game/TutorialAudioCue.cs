using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;

namespace Intersect.Client.Interface.Game;

/// <summary>One cue per authoritative transition, never per frame or polling packet.</summary>
internal sealed class TutorialAudioCue
{
    private Guid _step;
    private string? _revision;
    private bool _open;

    internal string Observe(TutorialStatePacket state, TutorialConfiguration config)
    {
        var previous = config.Steps.FindIndex(step => step.Id == _step);
        var current = config.Steps.FindIndex(step => step.Id == state.StepId);
        var file = "";
        if (_revision == state.Revision && _open && previous >= 0 &&
            (current > previous || state.Finished && state.StepId == Guid.Empty))
            file = state.Finished ? "royal_guide_complete_v1.wav" : "royal_guide_step_v1.wav";
        else if (state.Open && (_revision == null || _step != state.StepId))
            file = "royal_guide_objective_v1.wav";

        _step = state.StepId;
        _revision = state.Revision;
        _open = state.Open;
        return file;
    }
}
