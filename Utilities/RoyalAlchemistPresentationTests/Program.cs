using System.Drawing;
using System.Windows.Forms;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.MiniGames.Potions;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Arguments: repository root, preview output PNG");
        Directory.SetCurrentDirectory(args[0]);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var previous = RewardConfiguration.Instance.ToJson();
        try
        {
            RewardConfiguration.Load(new RewardConfiguration().ToJson());
            using (var form = new FrmRewardConfiguration())
            {
                form.NavigateGame(MiniGameType.Potions);
                var tabs = Descendants(form).OfType<TabControl>().Single(c => c.Name == "GamePages");
                var page = tabs.TabPages.Cast<TabPage>().Single(p => Descendants(p).OfType<PictureBox>().Count() == 2);
                tabs.SelectedTab = page;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-30000, -30000);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                form.PerformLayout();
                foreach (var child in Descendants(form)) child.CreateControl();
                Check(Descendants(page).OfType<PictureBox>().All(p => p.Image != null), "Both generated image previews load");
                var sounds = Descendants(page).OfType<EditorSoundPicker>().ToArray();
                Check(sounds.Length == 5, "All five sound selectors are accessible");
                sounds[0].FileName = "";
                form.NavigateGame(MiniGameType.Poker);
                form.NavigateGame(MiniGameType.Potions);
                tabs.SelectedTab = page;
                Check(sounds[0].FileName == "", "Draft choice survives navigation");
                Check(RewardConfiguration.Instance.PotionAssets.RotateSound.Length > 0, "Draft does not change live configuration");
                var save = Descendants(form).OfType<Button>().Single(b => b.Text == "Save all changes");
                void CheckFooter()
                {
                    var position = form.PointToClient(save.PointToScreen(Point.Empty));
                    Check(save.Visible && form.ClientRectangle.Contains(new Rectangle(position, save.Size)), "Save stays visible outside scrolling fields");
                    Check(!Descendants(form).Any(c => c.Name == "EditorScrollViewport"), "No legacy outer scrolling canvas");
                }
                CheckFooter();
                var originalSize = form.Size;
                form.Size = new Size(960, 640); Application.DoEvents(); CheckFooter();
                form.Size = originalSize; Application.DoEvents();
                using var screenshot = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(screenshot, new Rectangle(0, 0, form.Width, form.Height));
                screenshot.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                form.Hide();
            }
            RewardConfiguration.Load(new RewardConfiguration { PotionAssets = new PotionPresentationAssets { Background = "missing_workshop.png", RotateSound = "missing_rotate.wav" } }.ToJson());
            using (var form = new FrmRewardConfiguration())
            {
                form.NavigateGame(MiniGameType.Potions);
                Check(Descendants(form).OfType<ComboBox>().Any(c => c.SelectedItem?.ToString() == "missing_workshop.png"), "Missing image reference remains selected");
                Check(Descendants(form).OfType<EditorSoundPicker>().Any(c => c.FileName == "missing_rotate.wav"), "Missing sound reference remains selected");
            }
            Console.WriteLine("PASS: image previews, five sounds, navigation, unapplied drafts and missing-file preservation.");
            return 0;
        }
        finally { RewardConfiguration.Load(previous); }
    }
    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
