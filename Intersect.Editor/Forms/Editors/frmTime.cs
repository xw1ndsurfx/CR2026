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
    private readonly DateTimePicker _sunrise = PhasePicker();
    private readonly DateTimePicker _day = PhasePicker();
    private readonly DateTimePicker _sunset = PhasePicker();
    private readonly DateTimePicker _night = PhasePicker();

    private static DateTimePicker PhasePicker() => new()
    {
        Width = 105, Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm", ShowUpDown = true,
    };

    private static int Minutes(DateTimePicker picker) => picker.Value.Hour * 60 + picker.Value.Minute;
    private static void SetMinutes(DateTimePicker picker, int minutes) =>
        picker.Value = DateTime.Today.AddMinutes(Math.Clamp(minutes, 0, 1439));

    private void InitializePhases()
    {
        AutoSize = false;
        ClientSize = new Size(940, 370);
        MinimumSize = new Size(960, 410);
        btnSave.Location = new System.Drawing.Point(693, 333);
        btnCancel.Location = new System.Drawing.Point(814, 333);
        var box = new DarkGroupBox
        {
            Text = "Day Phases  |  Sunrise / Day / Sunset / Night",
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            BorderColor = System.Drawing.Color.FromArgb(90, 90, 90),
            ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(615, 25),
            Size = new Size(313, 300),
        };
        box.Controls.Add(new Label
        {
            Text = "Start time of each phase:", ForeColor = System.Drawing.Color.Gainsboro,
            Location = new System.Drawing.Point(14, 30), AutoSize = true,
        });

        void Row(string label, DateTimePicker picker, int y)
        {
            box.Controls.Add(new Label
            {
                Text = label, ForeColor = System.Drawing.Color.Gainsboro,
                Location = new System.Drawing.Point(16, y + 3), Size = new Size(135, 24),
            });
            picker.Location = new System.Drawing.Point(167, y);
            box.Controls.Add(picker);
        }

        Row("Sunrise", _sunrise, 68);
        Row("Day", _day, 111);
        Row("Sunset", _sunset, 154);
        Row("Night", _night, 197);
        box.Controls.Add(new Label
        {
            Text = "Night continues through midnight. Keep the phase starts in chronological order.",
            ForeColor = System.Drawing.Color.Silver,
            Location = new System.Drawing.Point(16, 243), Size = new Size(279, 45),
        });
        Controls.Add(box);
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
        //Create a backup in case we want to revert
        mYTime = time;
        mBackupTime = new DaylightCycleDescriptor();
        mBackupTime.LoadFromJson(time.GetInstanceJson());

        mTileBackbuffer = new Bitmap(pnlColor.Width, pnlColor.Height);
        UpdateList(DaylightCycleDescriptor.GetTimeInterval(cmbIntervals.SelectedIndex));
        typeof(Panel).InvokeMember(
            "DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null,
            pnlColor, new object[] {true}
        );

        chkSync.Checked = mYTime.SyncTime;
        txtTimeRate.Text = mYTime.Rate.ToString();
        cmbIntervals.SelectedIndex = DaylightCycleDescriptor.GetIntervalIndex(mYTime.RangeInterval);
        UpdateList(mYTime.RangeInterval);
        txtTimeRate.Enabled = !mYTime.SyncTime;
        var phases = mYTime.DayPhases ?? new DayPhaseSchedule();
        SetMinutes(_sunrise, phases.SunriseStartMinutes);
        SetMinutes(_day, phases.DayStartMinutes);
        SetMinutes(_sunset, phases.SunsetStartMinutes);
        SetMinutes(_night, phases.NightStartMinutes);
    }

    private void cmbIntervals_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (mYTime.RangeInterval != DaylightCycleDescriptor.GetTimeInterval(cmbIntervals.SelectedIndex))
        {
            mYTime.RangeInterval = DaylightCycleDescriptor.GetTimeInterval(cmbIntervals.SelectedIndex);
            UpdateList(mYTime.RangeInterval);
            mYTime.ResetColors();
            grpRangeOptions.Hide();
        }
    }

    private void UpdateList(int duration)
    {
        lstTimes.Items.Clear();
        var time = new DateTime(2000, 1, 1, 0, 0, 0);
        for (var i = 0; i < 1440; i += duration)
        {
            var addRange = time.ToString("h:mm:ss tt") + " " + Strings.TimeEditor.to + " ";
            time = time.AddMinutes(duration);
            addRange += time.ToString("h:mm:ss tt");
            lstTimes.Items.Add(addRange);
        }
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
        var phases = new DayPhaseSchedule
        {
            SunriseStartMinutes = Minutes(_sunrise),
            DayStartMinutes = Minutes(_day),
            SunsetStartMinutes = Minutes(_sunset),
            NightStartMinutes = Minutes(_night),
        };
        if (!phases.IsValid)
        {
            MessageBox.Show(this, "Expected Sunrise < Day < Sunset < Night; each phase must have a duration.",
                "Invalid day phases", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        mYTime.DayPhases = phases;
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
