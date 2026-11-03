using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmFishing
{
    private readonly Panel _fishingFeedback = new() { Dock = DockStyle.Top, Name = "FishingFeedback" };
    private readonly TextBox _activationState = new()
    {
        Name = "FishingActivationState", Multiline = true, ReadOnly = true, WordWrap = true,
        ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, TabStop = false,
        BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText,
    };
    private readonly Button _refreshActiveState = new() { Name = "FishingRefreshActiveState", AutoSize = true };
    private readonly Panel _applyFailure = new()
    {
        Name = "FishingApplyFailure", BackColor = DrawingColor.MistyRose, Visible = false,
    };
    private readonly Label _applyFailureTitle = new()
    {
        Name = "FishingApplyFailureTitle", AutoEllipsis = false, ForeColor = DrawingColor.Maroon,
    };
    private readonly TextBox _applyFailureMessage = new()
    {
        Name = "FishingApplyFailureMessage", Multiline = true, ReadOnly = true, WordWrap = true,
        ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, TabStop = false,
        BackColor = DrawingColor.MistyRose, ForeColor = DrawingColor.Maroon,
    };
    private bool _applicationFailureVisible, _layingOutFishingFeedback;

    private void InitializeFishingFeedback()
    {
        _refreshActiveState.Text = T("Refresh active configuration");
        _refreshActiveState.Click += (_, _) => FishingEditorState.Request();
        _applyFailure.Controls.Add(_applyFailureTitle);
        _applyFailure.Controls.Add(_applyFailureMessage);
        _fishingFeedback.Controls.Add(_activationState);
        _fishingFeedback.Controls.Add(_refreshActiveState);
        _fishingFeedback.Controls.Add(_applyFailure);
        _fishingFeedback.SizeChanged += (_, _) => LayoutFishingFeedback();
        SizeChanged += (_, _) => LayoutFishingFeedback();
        FishingEditorState.Changed += ActiveFishingConfigurationChanged;
        Disposed += (_, _) => FishingEditorState.Changed -= ActiveFishingConfigurationChanged;
        RefreshFishingActivationState();
    }

    private void ActiveFishingConfigurationChanged()
    {
        if (IsDisposed || Disposing) return;
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke((Action)RefreshFishingActivationState);
            return;
        }
        RefreshFishingActivationState();
    }

    private void RefreshFishingActivationState()
    {
        if (IsDisposed || Disposing) return;
        var lines = new List<string>();
        if (_zone is { } zone)
        {
            lines.Add(string.Format(T("Selected zone: {0}"), zone.Name));
            lines.Add(string.Format(T("Draft: {0}"), T(zone.Enabled ? "Enabled" : "Disabled")));
            var activeZone = FishingEditorState.HasConfiguration
                ? FishingEditorState.Configuration.Zones.FirstOrDefault(candidate => candidate.Id == zone.Id)
                : null;
            var activeState = !FishingEditorState.HasConfiguration
                ? T("Unknown — no active configuration received.")
                : activeZone is null
                    ? T("Absent from the active configuration.")
                    : T(activeZone.Enabled ? "Enabled" : "Disabled");
            lines.Add(string.Format(T("Last confirmed on server: {0}"), activeState));
        }
        else lines.Add(T("No zone selected."));
        if (FishingEditorState.IsRefreshing)
            lines.Add(T("Refreshing the active configuration..."));
        if (!string.IsNullOrWhiteSpace(FishingEditorState.LastError))
            lines.Add(string.Format(T("Active configuration update failed: {0}"), FishingEditorState.LastError));
        _activationState.Text = string.Join(Environment.NewLine, lines);
        _refreshActiveState.Enabled = !FishingEditorState.IsRefreshing;
        LayoutFishingFeedback();
    }

    private void ClearApplicationFailure()
    {
        _applicationFailureVisible = false;
        _applyFailure.Visible = false;
        _applyFailureTitle.Text = "";
        _applyFailureMessage.Text = "";
        LayoutFishingFeedback();
    }

    private void ShowApplicationFailure(string reason, bool timedOut = false)
    {
        _applyFailureTitle.Text = T(timedOut ? "Application acknowledgement timed out" : "Apply to game refused");
        _applyFailureMessage.Text = reason;
        _applicationFailureVisible = true;
        _applyFailure.Visible = true;
        LayoutFishingFeedback();
    }

    private void SaveTimedOut()
    {
        _saveTimeout.Stop();
        if (_pendingSaveJson is null) return;
        if (!_pendingSaveIsDraft)
            ShowApplicationFailure(T("The server did not acknowledge the application before the timeout. Refresh the active configuration to check its current state. Your draft is preserved."), timedOut: true);
        _save.Enabled = _apply.Enabled = true;
        _pendingSaveJson = null;
        _status.Text = T("No server response. Your draft is still available.");
        RefreshFishingActivationState();
    }

    private void LayoutFishingFeedback()
    {
        if (_layingOutFishingFeedback || _fishingFeedback.IsDisposed) return;
        _layingOutFishingFeedback = true;
        try
        {
            const int padding = 8, gap = 8;
            var width = Math.Max(200, _fishingFeedback.ClientSize.Width - padding * 2);
            var buttonSize = _refreshActiveState.GetPreferredSize(Size.Empty);
            var buttonWidth = Math.Min(width / 2, Math.Max(80, buttonSize.Width));
            var stateWidth = Math.Max(80, width - buttonWidth - gap);
            var stateHeight = FeedbackTextHeight(_activationState, stateWidth, 116);
            _activationState.SetBounds(padding, padding, stateWidth, stateHeight);
            _refreshActiveState.SetBounds(padding + stateWidth + gap, padding, buttonWidth, buttonSize.Height);
            var height = padding + Math.Max(stateHeight, buttonSize.Height) + padding;
            if (_applicationFailureVisible)
            {
                var innerWidth = width - padding * 2;
                var titleHeight = TextRenderer.MeasureText(_applyFailureTitle.Text, _applyFailureTitle.Font,
                    new Size(innerWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                var messageHeight = FeedbackTextHeight(_applyFailureMessage, innerWidth,
                    Math.Clamp(ClientSize.Height / 4, 80, 180));
                var failureHeight = padding * 2 + titleHeight + 4 + messageHeight;
                _applyFailure.SetBounds(padding, height, width, failureHeight);
                _applyFailureTitle.SetBounds(padding, padding, innerWidth, titleHeight);
                _applyFailureMessage.SetBounds(padding, padding + titleHeight + 4, innerWidth, messageHeight);
                height += failureHeight + padding;
            }
            if (_fishingFeedback.Height != height) _fishingFeedback.Height = height;
        }
        finally { _layingOutFishingFeedback = false; }
    }

    private static int FeedbackTextHeight(TextBox textBox, int width, int maximumHeight)
    {
        var minimumHeight = textBox.Font.Height * 2 + 8;
        var measured = TextRenderer.MeasureText(textBox.Text, textBox.Font,
            new Size(Math.Max(40, width - SystemInformation.VerticalScrollBarWidth - 8), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
        // Keep the whole reason in a wrapping, scrollable text box even when it exceeds the available window height.
        return Math.Clamp(measured.Height + 8, minimumHeight, Math.Max(minimumHeight, maximumHeight));
    }
}
