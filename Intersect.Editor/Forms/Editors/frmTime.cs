using System.Reflection;
using DarkUI.Controls;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Editor.Networking;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;


public partial class FrmTime : Form
{

    private DaylightCycleDescriptor mBackupTime;

    private Bitmap mTileBackbuffer;

    private DaylightCycleDescriptor mYTime;
    private bool _saved;
    private bool _initializing;
    private bool _updatingList;
    private bool _updatingPhaseCheckboxes;

    private readonly DarkCheckBox _sunrise = PhaseCheckbox("Sunrise");
    private readonly DarkCheckBox _day = PhaseCheckbox("Day");
    private readonly DarkCheckBox _sunset = PhaseCheckbox("Sunset");
    private readonly DarkCheckBox _night = PhaseCheckbox("Night");

    private Label _selectedRangeLabel;
    private Label _phaseCountsLabel;
    private readonly List<string> _timeLabels = [];

    private static DarkCheckBox PhaseCheckbox(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(155, 25),
        ForeColor = System.Drawing.Color.Gainsboro,
        BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
    };

    private void InitializePhases()
    {
        // Use docked columns instead of pixel offsets: WinForms font/DPI
        // scaling previously made the phase group overlap Time Settings,
        // hiding the beginning of its caption.
        SuspendLayout();
        try
        {
            AutoSize = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1150, 470);
            MinimumSize = new Size(1010, 465);

            // Reparent existing designer controls without recreating their
            // event bindings or disturbing overlay, rate and sync logic.
            Controls.Clear();

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 59,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(14, 10, 14, 9),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            };
            btnCancel.Size = new Size(130, 34);
            btnSave.Size = new Size(130, 34);
            btnCancel.Margin = new Padding(6, 0, 0, 0);
            btnSave.Margin = new Padding(6, 0, 0, 0);
            footer.Controls.Add(btnCancel);
            footer.Controls.Add(btnSave);

            var columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(14, 12, 14, 4),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var times = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Margin = new Padding(0, 0, 12, 0),
            };
            times.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            times.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            lblTimes.Dock = DockStyle.Fill;
            lblTimes.TextAlign = ContentAlignment.MiddleLeft;
            lstTimes.Dock = DockStyle.Fill;
            lstTimes.IntegralHeight = false;
            times.Controls.Add(lblTimes, 0, 0);
            times.Controls.Add(lstTimes, 0, 1);

            var settings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 8, 12, 0),
            };
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 192));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 127));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grpSettings.Dock = DockStyle.Fill;
            grpSettings.Margin = new Padding(0, 0, 0, 9);
            grpRangeOptions.Dock = DockStyle.Fill;
            grpRangeOptions.Margin = new Padding(0, 0, 0, 0);
            settings.Controls.Add(grpSettings, 0, 0);
            settings.Controls.Add(grpRangeOptions, 0, 1);

            var box = new DarkGroupBox
            {
                Text = "Day Phase - Selected Time Range",
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                BorderColor = System.Drawing.Color.FromArgb(90, 90, 90),
                ForeColor = System.Drawing.Color.Gainsboro,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
            };

            var phaseLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(14, 20, 14, 12),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            };
            phaseLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            phaseLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            phaseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            phaseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            phaseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            phaseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            phaseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            phaseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 69));
            phaseLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _selectedRangeLabel = new Label
            {
                Text = "Select a time range on the left.",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = System.Drawing.Color.Khaki,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 0, 4, 1),
            };
            phaseLayout.Controls.Add(_selectedRangeLabel, 0, 0);
            phaseLayout.SetColumnSpan(_selectedRangeLabel, 2);

            var instruction = new Label
            {
                Text = "Choose one phase for the selected range:",
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Gainsboro,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 0, 0, 0),
            };
            phaseLayout.Controls.Add(instruction, 0, 1);
            phaseLayout.SetColumnSpan(instruction, 2);

            var choices = new (DarkCheckBox Check, DayPhase Phase, int X, int Y)[]
            {
                (_sunrise, DayPhase.Sunrise, 0, 2),
                (_day, DayPhase.Day, 1, 2),
                (_sunset, DayPhase.Sunset, 0, 3),
                (_night, DayPhase.Night, 1, 3),
            };
            foreach (var (check, phase, column, row) in choices)
            {
                check.Dock = DockStyle.Fill;
                check.Margin = new Padding(6, 4, 5, 4);
                check.Enabled = false;
                check.CheckedChanged += (_, _) => PhaseCheckboxChanged(check, phase);
                phaseLayout.Controls.Add(check, column, row);
            }

            var summaryTitle = new Label
            {
                Text = "RANGES PER PHASE",
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Khaki,
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 0, 0, 0),
            };
            phaseLayout.Controls.Add(summaryTitle, 0, 4);
            phaseLayout.SetColumnSpan(summaryTitle, 2);

            _phaseCountsLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.LightSteelBlue,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(6, 1, 6, 1),
            };
            phaseLayout.Controls.Add(_phaseCountsLabel, 0, 5);
            phaseLayout.SetColumnSpan(_phaseCountsLabel, 2);

            var tip = new Label
            {
                Text = "Each interval has one phase. You may use multiple " +
                       "days or nights in the same 24-hour cycle.",
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.Silver,
                TextAlign = ContentAlignment.TopLeft,
                Margin = new Padding(4, 5, 4, 0),
            };
            phaseLayout.Controls.Add(tip, 0, 6);
            phaseLayout.SetColumnSpan(tip, 2);

            box.Controls.Add(phaseLayout);
            columns.Controls.Add(times, 0, 0);
            columns.Controls.Add(settings, 1, 0);
            columns.Controls.Add(box, 2, 0);

            Controls.Add(columns);
            Controls.Add(footer);
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
    }

    private void PhaseCheckboxChanged(DarkCheckBox checkbox, DayPhase phase)
    {
        if (_initializing || _updatingPhaseCheckboxes ||
            mYTime == null || lstTimes.SelectedIndex < 0)
            return;

        // Checkboxes behave like an exclusive selection: every slot always has
        // exactly one phase. Unchecking the active option keeps it selected.
        if (!checkbox.Checked)
        {
            RefreshPhaseCheckboxes();
            return;
        }

        var index = lstTimes.SelectedIndex;
        mYTime.DayPhases ??= new DayPhaseSchedule();
        mYTime.DayPhases.ConfigureIntervals(mYTime.RangeInterval);
        mYTime.DayPhases.SetPhaseForInterval(index, phase);
        RefreshTimeSlotText(index);
        RefreshPhaseCheckboxes();
    }

    private void RefreshPhaseCheckboxes()
    {
        if (_updatingPhaseCheckboxes)
            return;

        _updatingPhaseCheckboxes = true;
        try
        {
            var index = lstTimes.SelectedIndex;
            var ready = mYTime?.DayPhases != null &&
                        index >= 0 && index < _timeLabels.Count;

            _selectedRangeLabel.Text = ready
                ? _timeLabels[index]
                : "Select a time range on the left.";

            var selected = ready
                ? mYTime.DayPhases.GetPhaseAtMinute(index * mYTime.RangeInterval)
                : DayPhase.Night;

            foreach (var (check, phase) in new[]
            {
                (_sunrise, DayPhase.Sunrise),
                (_day, DayPhase.Day),
                (_sunset, DayPhase.Sunset),
                (_night, DayPhase.Night),
            })
            {
                check.Enabled = ready;
                check.Checked = ready && selected == phase;
            }

            if (mYTime?.DayPhases?.IntervalPhases is { } phases)
            {
                _phaseCountsLabel.Text =
                    $"Sunrise: {phases.Count(p => p == DayPhase.Sunrise)}        " +
                    $"Day: {phases.Count(p => p == DayPhase.Day)}\n" +
                    $"Sunset: {phases.Count(p => p == DayPhase.Sunset)}        " +
                    $"Night: {phases.Count(p => p == DayPhase.Night)}";
            }
            else
            {
                _phaseCountsLabel.Text = string.Empty;
            }
        }
        finally
        {
            _updatingPhaseCheckboxes = false;
        }
    }

    private string TimeSlotText(int index)
    {
        var minute = index * mYTime.RangeInterval;
        var phase = mYTime.DayPhases.GetPhaseAtMinute(minute);
        return _timeLabels[index] + "  [" + phase + "]";
    }

    private void RefreshTimeSlotText(int index)
    {
        if (index < 0 || index >= _timeLabels.Count)
            return;

        var top = lstTimes.TopIndex;
        _updatingList = true;
        try
        {
            lstTimes.Items[index] = TimeSlotText(index);
            lstTimes.SelectedIndex = index;
            lstTimes.TopIndex = top;
        }
        finally
        {
            _updatingList = false;
        }
    }

    private static Intersect.Color[] ResampleOverlay(
        Intersect.Color[]? previous, int previousMinutes, int newMinutes)
    {
        var newColors = new Intersect.Color[1440 / newMinutes];
        for (var index = 0; index < newColors.Length; index++)
        {
            var original = previousMinutes > 0
                ? index * newMinutes / previousMinutes
                : -1;

            var source = previous != null && original >= 0 && original < previous.Length
                ? previous[original]
                : null;

            // The tint colors are mutable objects: never share one instance
            // between multiple new intervals.
            newColors[index] = source == null
                ? new Intersect.Color(255, 255, 255, 255)
                : new Intersect.Color(source);
        }
        return newColors;
    }

    public FrmTime()
    {
        InitializeComponent();
        InitializePhases();
        InitLocalization();
    }

    private void InitLocalization()
    {
        Text = Strings.TimeEditor.title;
        lblTimes.Text = Strings.TimeEditor.times;
        grpSettings.Text = Strings.TimeEditor.settings;
        lblIntervals.Text = Strings.TimeEditor.interval;
        cmbIntervals.Items.Clear();
        for (var i = 0; i < Strings.TimeEditor.intervals.Count; i++)
        {
            cmbIntervals.Items.Add(Strings.TimeEditor.intervals[i]);
        }

        chkSync.Text = Strings.TimeEditor.sync;
        lblRate.Text = Strings.TimeEditor.rate;
        lblRateSuffix.Text = Strings.TimeEditor.ratesuffix;
        lblRateDesc.Text = Strings.TimeEditor.ratedesc;
        grpRangeOptions.Text = Strings.TimeEditor.overlay;
        lblColorDesc.Text = Strings.TimeEditor.colorpaneldesc;
        btnSave.Text = Strings.TimeEditor.save;
        btnCancel.Text = Strings.TimeEditor.cancel;
    }

    public void InitEditor(DaylightCycleDescriptor time)
    {
        // Back up all values so Cancel restores the previous phase assignments,
        // interval, and tint colors without persisting any accidental edits.
        mYTime = time;
        mBackupTime = new DaylightCycleDescriptor();
        mBackupTime.LoadFromJson(time.GetInstanceJson());

        mTileBackbuffer = new Bitmap(pnlColor.Width, pnlColor.Height);
        typeof(Panel).InvokeMember(
            "DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance |
            BindingFlags.NonPublic, null, pnlColor, new object[] { true }
        );

        _initializing = true;
        try
        {
            chkSync.Checked = mYTime.SyncTime;
            txtTimeRate.Text = mYTime.Rate.ToString();
            cmbIntervals.SelectedIndex =
                DaylightCycleDescriptor.GetIntervalIndex(mYTime.RangeInterval);
            txtTimeRate.Enabled = !mYTime.SyncTime;
        }
        finally
        {
            _initializing = false;
        }

        mYTime.DayPhases ??= new DayPhaseSchedule();
        mYTime.DayPhases.ConfigureIntervals(mYTime.RangeInterval);

        if (mYTime.DaylightHues?.Length != 1440 / mYTime.RangeInterval)
            mYTime.DaylightHues = ResampleOverlay(mYTime.DaylightHues,
                mYTime.RangeInterval, mYTime.RangeInterval);

        UpdateList(mYTime.RangeInterval, 0);
    }

    private void cmbIntervals_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_initializing || mYTime == null || cmbIntervals.SelectedIndex < 0)
            return;

        var newInterval = DaylightCycleDescriptor.GetTimeInterval(cmbIntervals.SelectedIndex);
        if (mYTime.RangeInterval == newInterval)
            return;

        var oldInterval = mYTime.RangeInterval;
        var selectedMinute = Math.Max(0, lstTimes.SelectedIndex) * oldInterval;
        var resampledColors = ResampleOverlay(mYTime.DaylightHues, oldInterval, newInterval);

        mYTime.DayPhases ??= new DayPhaseSchedule();
        mYTime.DayPhases.ConfigureIntervals(newInterval);
        mYTime.RangeInterval = newInterval;
        mYTime.DaylightHues = resampledColors;
        UpdateList(newInterval, selectedMinute);
    }

    private void UpdateList(int duration, int selectedMinute = 0)
    {
        if (mYTime == null || duration <= 0)
            return;

        _updatingList = true;
        lstTimes.BeginUpdate();
        try
        {
            lstTimes.Items.Clear();
            _timeLabels.Clear();
            var time = new DateTime(2000, 1, 1, 0, 0, 0);
            for (var minute = 0; minute < 1440; minute += duration)
            {
                var start = time.ToString("h:mm tt");
                time = time.AddMinutes(duration);
                var label = start + " - " + time.ToString("h:mm tt");
                _timeLabels.Add(label);
                lstTimes.Items.Add(TimeSlotText(_timeLabels.Count - 1));
            }

            lstTimes.SelectedIndex = Math.Clamp(selectedMinute / duration, 0, lstTimes.Items.Count - 1);
        }
        finally
        {
            lstTimes.EndUpdate();
            _updatingList = false;
        }

        lstTimes_SelectedIndexChanged(lstTimes, EventArgs.Empty);
    }

    private void pnlColor_DoubleClick(object sender, EventArgs e)
    {
        clrSelector.Color = pnlColor.BackColor;
        if (clrSelector.ShowDialog() == DialogResult.OK)
        {
            pnlColor.BackColor = clrSelector.Color;
            mYTime.DaylightHues[lstTimes.SelectedIndex].R = pnlColor.BackColor.R;
            mYTime.DaylightHues[lstTimes.SelectedIndex].G = pnlColor.BackColor.G;
            mYTime.DaylightHues[lstTimes.SelectedIndex].B = pnlColor.BackColor.B;
        }
    }

    private void pnlColor_Paint(object sender, PaintEventArgs e)
    {
        var g = System.Drawing.Graphics.FromImage(mTileBackbuffer);
        g.Clear(System.Drawing.Color.Transparent);
        g.DrawImage(pnlColor.BackgroundImage, new System.Drawing.Point(0, 0));
        Brush brush = new SolidBrush(
            System.Drawing.Color.FromArgb(
                scrlAlpha.Value, pnlColor.BackColor.R, pnlColor.BackColor.G, pnlColor.BackColor.B
            )
        );

        g.FillRectangle(brush, new Rectangle(0, 0, pnlColor.Width, pnlColor.Height));
        e.Graphics.DrawImage(mTileBackbuffer, new System.Drawing.Point(0, 0));
    }

    private void scrlAlpha_Scroll(object sender, ScrollValueEventArgs e)
    {
        var brightness = (int) ((255 - scrlAlpha.Value) / 255f * 100);
        lblBrightness.Text = Strings.TimeEditor.brightness.ToString(brightness.ToString());
        mYTime.DaylightHues[lstTimes.SelectedIndex].A = (byte) scrlAlpha.Value;
        pnlColor.Refresh();
    }

    private void lstTimes_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_updatingList || _initializing || mYTime == null)
            return;

        RefreshPhaseCheckboxes();
        if (lstTimes.SelectedIndex == -1)
        {
            grpRangeOptions.Hide();

            return;
        }

        grpRangeOptions.Show();
        pnlColor.BackColor = System.Drawing.Color.FromArgb(
            255, mYTime.DaylightHues[lstTimes.SelectedIndex].R, mYTime.DaylightHues[lstTimes.SelectedIndex].G,
            mYTime.DaylightHues[lstTimes.SelectedIndex].B
        );

        scrlAlpha.Value = mYTime.DaylightHues[lstTimes.SelectedIndex].A;
        var brightness = (int) ((255 - scrlAlpha.Value) / 255f * 100);
        lblBrightness.Text = Strings.TimeEditor.brightness.ToString(brightness);
        pnlColor.Refresh();
        Core.Graphics.LightColor = mYTime.DaylightHues[lstTimes.SelectedIndex];
    }

    private void chkSync_CheckedChanged(object sender, EventArgs e)
    {
        mYTime.SyncTime = chkSync.Checked;
        txtTimeRate.Enabled = !mYTime.SyncTime;
    }

    private void txtTimeRate_TextChanged(object sender, EventArgs e)
    {
        if (float.TryParse(txtTimeRate.Text, out var val))
        {
            mYTime.Rate = val;
        }
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        mYTime.DayPhases ??= new DayPhaseSchedule();
        mYTime.DayPhases.ConfigureIntervals(mYTime.RangeInterval);
        PacketSender.SendSaveTime(mYTime.GetInstanceJson());
        _saved = true;
        Hide();
        Globals.CurrentEditor = -1;
        Dispose();
    }

    private void FrmTime_FormClosed(object sender, FormClosedEventArgs e)
    {
        if (!_saved && mYTime != null && mBackupTime != null)
            mYTime.LoadFromJson(mBackupTime.GetInstanceJson());
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        if (!_saved && mYTime != null && mBackupTime != null)
            mYTime.LoadFromJson(mBackupTime.GetInstanceJson());
        Hide();
        Globals.CurrentEditor = -1;
        Dispose();
    }

}
