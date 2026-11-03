using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

internal sealed class FrmProjectileAutoCreator : Form
{
    private sealed record AnimationChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly TextBox _name = new() { Name = "ProjectileName" };
    private readonly ComboBox _pattern = new() { Name = "ProjectilePattern", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _behavior = new() { Name = "ProjectileBehavior", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _animation = new() { Name = "ProjectileAnimation", DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 420 };
    private readonly NumericUpDown _range = Number("ProjectileRange", 1, 100);
    private readonly NumericUpDown _travel = Number("ProjectileTravelTime", 1, 60000);
    private readonly NumericUpDown _bursts = Number("ProjectileBursts", 1, 20);
    private readonly NumericUpDown _delay = Number("ProjectileDelay", 1, 60000);
    private readonly NumericUpDown _knockback = Number("ProjectileKnockback", 0, 100);
    private readonly CheckBox _rotate = new() { Name = "ProjectileAutoRotate" };
    private readonly CheckBox _pierce = new() { Name = "ProjectilePierce" };
    private readonly CheckBox _ignoreBlocks = new() { Name = "ProjectileIgnoreBlocks" };
    private readonly Label _summary = new() { Name = "ProjectileSummary", AutoSize = false };
    private readonly PictureBox _image = new() { Name = "ProjectilePreview", SizeMode = PictureBoxSizeMode.Zoom, BackColor = System.Drawing.Color.FromArgb(30, 30, 33) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 80 };
    private EditorObjectPreview? _preview;
    internal ProjectileAutoProfile? Result { get; private set; }
    internal string ProjectileName => _name.Text.Trim();
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private static NumericUpDown Number(string name, int minimum, int maximum) => new() { Name = name, Minimum = minimum, Maximum = maximum };

    internal FrmProjectileAutoCreator(ProjectileDescriptor source)
    {
        Text = T("Auto Projectile Creator"); Size = new(620, 780); MinimumSize = new(460, 390);
        AutoScroll = true; StartPosition = FormStartPosition.CenterParent; MaximizeBox = true;
        BackColor = System.Drawing.Color.FromArgb(45, 45, 48); ForeColor = System.Drawing.Color.Gainsboro;
        var y = 16;
        void Row(string caption, Control control)
        {
            var label = new Label { Text = T(caption), Location = new(18, y + 3), Size = new(234, 28) };
            control.SetBounds(260, y, 300, 26); Controls.AddRange([label, control]); y += 38;
        }
        _name.Text = source.Name;
        _pattern.Items.AddRange(new object[] { T("Straight shot"), T("Three-way shot"), T("Five-way shot"), T("Radial burst") }); _pattern.SelectedIndex = 0;
        _behavior.Items.AddRange(new object[] { T("Directional"), T("Direct shot"), T("Homing") });
        _behavior.SelectedIndex = source.HomingBehavior ? 2 : source.DirectShotBehavior ? 1 : 0;
        _animation.Items.Add(new AnimationChoice(Guid.Empty, T("None")));
        foreach (var animation in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name))
            _animation.Items.Add(new AnimationChoice(animation.Id, animation.Name));
        var current = source.Animations.FirstOrDefault();
        if (current != null && !_animation.Items.Cast<AnimationChoice>().Any(a => a.Id == current.AnimationId))
            _animation.Items.Add(new AnimationChoice(current.AnimationId, T("Missing animation")));
        _animation.SelectedItem = _animation.Items.Cast<AnimationChoice>().First(a => a.Id == (current?.AnimationId ?? Guid.Empty));
        _range.Value = Math.Clamp(source.Range > 1 ? source.Range : 8, 1, 100);
        _travel.Value = Math.Clamp(source.Speed > 1 ? source.Speed : 600, 1, 60000);
        _bursts.Value = Math.Clamp(source.Quantity, 1, 20); _delay.Value = Math.Clamp(source.Delay > 1 ? source.Delay : 120, 1, 60000);
        _knockback.Value = Math.Clamp(source.Knockback, 0, 100);
        Row("Projectile name", _name); Row("Shot pattern", _pattern); Row("Targeting behavior", _behavior);
        Row("Projectile animation", _animation); Row("Range (tiles)", _range); Row("Travel time (ms)", _travel);
        Row("Burst count", _bursts); Row("Time between bursts (ms)", _delay); Row("Knockback (tiles)", _knockback);
        foreach (var (control, text, value) in new[] { (_rotate, "Rotate animation with direction", current?.AutoRotate ?? true),
            (_pierce, "Pierce targets", source.PierceTarget), (_ignoreBlocks, "Pass through map blocks", source.IgnoreMapBlocks) })
        {
            control.Text = T(text); control.Checked = value; control.SetBounds(18, y, 542, 26); Controls.Add(control); y += 30;
        }
        _summary.SetBounds(18, y + 6, 410, 126); _image.SetBounds(444, y + 6, 116, 116); Controls.AddRange([_summary, _image]); y += 142;
        var note = new Label { Text = T("Damage comes from the weapon or spell using this projectile. Ammo, collision spells and grapple settings are preserved."),
            Location = new(18, y), Size = new(542, 60), ForeColor = System.Drawing.Color.Khaki };
        Controls.Add(note); y += 68;
        var apply = new Button { Name = "ApplyProjectileProfile", Text = T("Apply to projectile"), Location = new(322, y), Size = new(238, 36) };
        var cancel = new Button { Text = T("Cancel"), DialogResult = DialogResult.Cancel, Location = new(18, y), Size = new(150, 36) };
        Controls.AddRange([apply, cancel]); AcceptButton = apply; CancelButton = cancel; AutoScrollMinSize = new(580, y + 60);
        apply.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(ProjectileName)) { MessageBox.Show(this, T("Enter a projectile name."), Text); return; }
            Result = Profile(); DialogResult = DialogResult.OK; Close();
        };
        foreach (var number in new[] { _range, _travel, _bursts, _delay, _knockback }) number.ValueChanged += (_, _) => RefreshSummary();
        _pattern.SelectedIndexChanged += (_, _) => RefreshSummary(); _behavior.SelectedIndexChanged += (_, _) => RefreshSummary();
        _animation.SelectedIndexChanged += (_, _) => RefreshAnimation();
        _timer.Tick += (_, _) => RenderFrame();
        RefreshSummary(); RefreshAnimation();
    }

    private ProjectileAutoProfile Profile() => new()
    {
        Pattern = (ProjectileAutoPattern)_pattern.SelectedIndex, Behavior = (ProjectileAutoBehavior)_behavior.SelectedIndex,
        Range = (int)_range.Value, TravelTime = (int)_travel.Value, Bursts = (int)_bursts.Value,
        Delay = (int)_delay.Value, Knockback = (int)_knockback.Value,
        AnimationId = (_animation.SelectedItem as AnimationChoice)?.Id ?? Guid.Empty,
        AutoRotate = _rotate.Checked, Pierce = _pierce.Checked, IgnoreMapBlocks = _ignoreBlocks.Checked,
    };

    private void RefreshSummary()
    {
        var p = Profile();
        _summary.Text = string.Format(T("{0} projectile(s) per burst × {1} burst(s) = {2} total\nRange: {3} tiles | Travel: {4} ms\nDelay: {5} ms | Total sequence: {6} ms\nReview the preview, then Apply and Save in the editor."),
            p.Rays, p.Bursts, p.Rays * p.Bursts, p.Range, p.TravelTime, p.Delay, p.TravelTime + (p.Bursts - 1) * p.Delay);
    }

    private void RefreshAnimation()
    {
        _timer.Stop(); _preview?.Dispose(); _preview = null;
        if (AnimationDescriptor.Get((_animation.SelectedItem as AnimationChoice)?.Id ?? Guid.Empty) is { } animation)
            _preview = EditorObjectPreview.Create(animation);
        RenderFrame();
        if (_preview?.Animated == true) _timer.Start();
    }
    private void RenderFrame() { var previous = _image.Image; _image.Image = _preview?.Render(); previous?.Dispose(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _preview?.Dispose(); _image.Image?.Dispose(); _image.Image = null; }
        base.Dispose(disposing);
    }
}
