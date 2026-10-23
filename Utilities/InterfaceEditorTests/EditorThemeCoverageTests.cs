using System.Reflection;
using Intersect.Editor.Forms;
using Intersect.Editor.Forms.Editors;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Editor.Forms.Styling;
using Intersect.GameObjects;
using Intersect.Framework.Core.GameObjects.Maps;

internal static class EditorThemeCoverageTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    internal static void Run()
    {
        var apply = typeof(FrmMain).Assembly.GetType("Intersect.Editor.Forms.Styling.EditorTheme", true)!
            .GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var create in new Func<Form>[]
        {
            () => new FrmItem(), () => new FrmNpc(), () => new FrmSpell(),
            () => new FrmEvent(new MapDescriptor(Guid.NewGuid())), () => new FrmCommonEvent(),
            () => new FrmSwitchVariable(), () => new FrmTime(), () => new FrmOptions(),
            () => new FrmTranslations(), () => new FrmAbout(), () => new UpdatePreview(),
        })
        {
            using var form = create();
            // Preview the real layouts without starting login/network/GPU load handlers.
            var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component)
                .GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var loadKey = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(object) && field.Name.Contains("load", StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
            if (events[loadKey] is EventHandler load) form.Load -= load;
            form.Opacity = 0; form.ShowInTaskbar = false;
            apply.Invoke(null, [form]); apply.Invoke(null, [form]);
            form.Show(); Application.DoEvents();
            Check(EditorWindows.SupportsFullscreen(form) && form.MaximizeBox,
                form.GetType().Name + " exposes maximize and shared fullscreen.");
            var originalBounds = form.Bounds;
            EditorWindows.ToggleFullscreen(form);
            Check(form.Bounds == Screen.FromControl(form).Bounds, form.GetType().Name + " fills its monitor.");
            EditorWindows.ToggleFullscreen(form);
            Check(form.Bounds == originalBounds, form.GetType().Name + " restores its previous bounds.");
            form.PerformLayout();
            var controls = Descendants(form).ToArray();
            foreach (var control in controls)
            {
                var type = control.GetType();
                Check(type.Namespace != "DarkUI.Controls" || type.Name is not
                    ("DarkButton" or "DarkComboBox" or "DarkCheckBox" or "DarkRadioButton" or "DarkGroupBox" or "DarkNumericUpDown"),
                    $"{form.Name}/{control.Name} still uses fixed legacy painting.");
            }
            if (Environment.GetEnvironmentVariable("CODEX_EDITOR_THEME_PREVIEW") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(Path.Combine(folder, (form is FrmUpdate ? nameof(FrmUpdate) : form.GetType().Name) + ".png"));
            }
            Console.WriteLine($"PASS shared editor theme: {form.GetType().Name}, {controls.Length} controls");
            form.Hide();
        }

        using var host = new Form { ClientSize = new Size(600, 420), Opacity = 0, ShowInTaskbar = false };
        var button = new LogiklikButton { Text = "Action", Bounds = new Rectangle(20, 20, 120, 32) };
        var check = new LogiklikCheckBox { Text = "Enabled", Bounds = new Rectangle(20, 65, 160, 26) };
        var radioA = new LogiklikRadioButton { Text = "A", Bounds = new Rectangle(20, 100, 120, 26), Checked = true };
        var radioB = new LogiklikRadioButton { Text = "B", Bounds = new Rectangle(155, 100, 120, 26) };
        var combo = new LogiklikComboBox { Bounds = new Rectangle(20, 140, 230, 26), DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(["First", "Second"]);
        var numeric = new LogiklikNumericUpDown { Bounds = new Rectangle(20, 180, 130, 26), Minimum = -100, Maximum = 100, Value = 42 };
        var progress = new LogiklikProgressBar { Bounds = new Rectangle(280, 180, 260, 20), Value = 42 };
        var tabs = new LogiklikTabControl { Bounds = new Rectangle(20, 220, 550, 150) };
        tabs.TabPages.Add("First tab"); tabs.TabPages.Add("A longer tab caption");
        host.Controls.AddRange([button, check, radioA, radioB, combo, numeric, progress, tabs]);
        var clicks = 0; button.Click += (_, _) => clicks++;
        apply.Invoke(null, [host]); apply.Invoke(null, [host]);
        host.Show(); Application.DoEvents();
        // Invoke input behavior through the original WinForms API, not the painters.
        button.PerformClick();
        check.Checked = true; radioB.Checked = true; combo.SelectedIndex = 1;
        Check(clicks == 1 && check.Checked && radioB.Checked && !radioA.Checked && combo.Text == "Second" && numeric.Value == 42 && progress.Value == 42,
            "Modern controls must preserve click handlers, checks, radio exclusivity, selection and numeric values.");
        Check(tabs.SizeMode == TabSizeMode.Normal, "Tab captions must use content width instead of a fixed 140px width.");
        tabs.SelectedIndex = 1;
        Check(tabs.SelectedTab == tabs.TabPages[1], "Modern tab painting must retain native tab selection.");
        var dynamic = new LogiklikButton { Text = "Later" }; host.Controls.Add(dynamic);
        Check(dynamic.BackColor == button.BackColor, "Dynamically created controls must receive the same palette.");
        using (var dialog = LogiklikMessageBox.Create("Preserve unsaved edits?", "Confirmation", MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2))
        {
            var actions = Descendants(dialog).OfType<Button>().ToArray();
            Check(actions.Length == 3 && ((Button)dialog.AcceptButton!).DialogResult == DialogResult.No &&
                ((Button)dialog.CancelButton!).DialogResult == DialogResult.Cancel,
                "Modern confirmations must preserve button results and the requested default/cancel actions.");
            dialog.Opacity = 0; dialog.Show(); Application.DoEvents();
            if (Environment.GetEnvironmentVariable("CODEX_EDITOR_THEME_PREVIEW") is { Length: > 0 } folder)
            {
                using var image = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                image.Save(Path.Combine(folder, "Confirmation.png"));
            }
            actions.Single(action => action.DialogResult == DialogResult.No).PerformClick();
            Check(dialog.DialogResult == DialogResult.No, "A negative confirmation must retain DialogResult.No.");
            dialog.Hide();
        }
        Console.WriteLine("PASS shared theme preserves control behavior and dynamic windows without duplicate handlers");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class UpdatePreview : FrmUpdate
    {
        // OnShown normally starts the real updater or login; this preview only renders it.
        protected override void OnShown(EventArgs e) { }
    }
}
