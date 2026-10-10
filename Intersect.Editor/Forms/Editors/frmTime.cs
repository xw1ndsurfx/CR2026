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
        // Room for the original time list, the overlay tools, and one compact
        // phase selector for the currently selected time interval.
        AutoSize = false;
        ClientSize = new Size(1034, 370);
        MinimumSize = new Size(1050, 410);
        lstTimes.Size = new Size(292, 275);
        grpSettings.Location = new System.Drawing.Point(322, 25);
        grpRangeOptions.Location = new System.Drawing.Point(322, 206);
        btnSave.Location = new System.Drawing.Point(780, 333);
        btnCancel.Location = new System.Drawing.Point(905, 333);

        var box = new DarkGroupBox
        {
            Text = "Phase for Selected Time Range",
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            BorderColor = System.Drawing.Color.FromArgb(90, 90, 90),
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(615, 25),
            Size = new Size(408, 300),
        };

        _selectedRangeLabel = new Label
        {
            Text = "Select a time range on the left.",
            ForeColor = System.Drawing.Color.Khaki,
            Location = new System.Drawing.Point(16, 28),
            Size = new Size(376, 31),
        };
        box.Controls.Add(_selectedRangeLabel);
        box.Controls.Add(new Label
        {
            Text = "Select one phase for this time range:",
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(16, 65),
            Size = new Size(373, 19),
        });

        var options = new (DarkCheckBox Check, DayPhase Phase, int X, int Y)[]
        {
            (_sunrise, DayPhase.Sunrise, 20, 101),
            (_day, DayPhase.Day, 210, 101),
            (_sunset, DayPhase.Sunset, 20, 145),
            (_night, DayPhase.Night, 210, 145),
        };
        foreach (var (check, phase, x, y) in options)
        {
            check.Location = new System.Drawing.Point(x, y);
            check.Enabled = false;
            check.CheckedChanged += (_, _) => PhaseCheckboxChanged(check, phase);
            box.Controls.Add(check);
        }

        _phaseCountsLabel = new Label
        {
            Text = string.Empty,
            ForeColor = System.Drawing.Color.LightSteelBlue,
            Location = new System.Drawing.Point(16, 193),
            Size = new Size(377, 39),
        };
        box.Controls.Add(_phaseCountsLabel);
        box.Controls.Add(new Label
        {
            Text = "Each time range has exactly one phase. You can repeat " +
                   "Night, Day, Sunrise or Sunset as often as you want.",
            ForeColor = System.Drawing.Color.Silver,
            Location = new System.Drawing.Point(16, 241),
            Size = new Size(376, 44),
        });

        Controls.Add(box);
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
                    $"Ranges: Sunrise {phases.Count(p => p == DayPhase.Sunrise)}   " +
                    $"Day {phases.Count(p => p == DayPhase.Day)}\n" +
                    $"Sunset {phases.Count(p => p == DayPhase.Sunset)}   " +
                    $"Night {phases.Count(p => p == DayPhase.Night)}";
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
