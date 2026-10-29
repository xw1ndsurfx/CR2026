using System.Globalization;
using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Localization;
using Console = System.Console;
using Control = Intersect.Client.Framework.Gwen.Control.Base;
using Point = Intersect.Point;
using Label = Intersect.Client.Framework.Gwen.Control.Label;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using SD = System.Drawing;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var output = args.Length > 1 ? args[1] : Path.Combine(root, "artifacts", "royal-contracts");
        Directory.CreateDirectory(output);
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, LogContextProxy>();
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        var content = new TestContentManager();
        foreach (var file in new[] { "board.png", "parchment.png" }) content.AddGuiTexture("royal-contracts/" + file, new BitmapTexture(Path.Combine(root, "resources", "gui", "royal-contracts", file)));
        var now = DateTimeOffset.UtcNow;
        var config = RoyalContractsConfiguration.CreateDefault(); var rotation = new RoyalContractRotation(); rotation.Refresh(config, now);
        var state = new RoyalContractBoardState { Enabled = true, Seals = 120, ServerTime = now };
        foreach (var c in rotation.Daily.Concat(rotation.Weekly))
            state.Contracts.Add(new() { Contract = c, Eligible = true, Accepted = true, Contribution = 2, Progress = c.Template.Collective ? 326 : 2, TargetName = c.Template.Objective == RoyalContractObjective.NpcKills ? "Any enemy" : "Distinct maps" });
        var checks = 0;
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks++; }
        var languageProperty = typeof(ClientLanguages).GetProperty(nameof(ClientLanguages.Current))!;
        foreach (var language in GameTextCatalog.Languages)
        {
            languageProperty.SetValue(null, language);
            foreach (var resolution in new[] { new Point(1024, 768), new Point(1280, 720), new Point(1920, 1080) })
            {
                using var renderer = new DrawingRenderer(resolution.X, resolution.Y);
                using var skin = new DrawingSkin(renderer);
                using var canvas = new Canvas(skin) { Size = resolution };
                var window = new RoyalContractsWindow(canvas); window.Apply(state); window.Show();
                canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero);
                canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero);
                Check(window.Width <= resolution.X && window.Height <= resolution.Y, "Board fits " + resolution);
                var all = All(window).ToArray();
                Check(all.OfType<Label>().Any(l => l.Text == RoyalContractsText.Get("Royal Contracts", language)), "Localized heading " + language);
                Check(all.OfType<Button>().Any(b => b.Text == RoyalContractsText.Get("In progress", language) && b.IsDisabled), "Incomplete hunt cannot claim");
                Check(all.OfType<ImagePanel>().Count(p => p.Texture != null) >= 5, "Both supplied image assets loaded");
                var cards = all.First(c => c.Name == "ContractCards");
                Check(cards.Children.Count == 3, "Three daily parchment cards");
                foreach (var child in cards.Children) Check(child.X >= 0 && child.Y >= 0 && child.Right <= cards.Width && child.Bottom <= cards.Height, "Card remains inside board");
                if (language == "fr" && resolution.X == 1280) renderer.Bitmap.Save(Path.Combine(output, "contrats-royaux-apercu.png"));
                // Long custom content and 8-slot pagination must not lose or overlap controls.
                var many = RoyalContractRules.Read<RoyalContractBoardState>(RoyalContractRules.Json(state));
                while (many.Contracts.Count(v => v.Contract.Template.Period == RoyalContractPeriod.Daily) < 8)
                {
                    var entry = RoyalContractRules.Read<RoyalContractView>(RoyalContractRules.Json(many.Contracts[0])); entry.Contract.Id = Guid.NewGuid(); entry.Contract.Template.Name = new string('W', 100); entry.Contract.Template.Description = string.Join(" ", Enumerable.Repeat("Une longue description à lire.", 20)); many.Contracts.Add(entry);
                }
                window.Apply(many);
                Check(All(window).First(c => c.Name == "ContractCards").Children.Count == 4, "Pagination limits to four cards");
                var pageField = typeof(RoyalContractsWindow).GetField("_page", BindingFlags.Instance | BindingFlags.NonPublic)!;
                pageField.SetValue(window, 1); window.Apply(many);
                Check(All(window).OfType<Label>().Any(l => l.Text == string.Format(RoyalContractsText.Get("Page {0} / {1}", language), 2, 2)), "Second page localized");
                canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero);
                var detailText = All(window).OfType<RichLabel>().Single();
                var detailScroll = All(window).OfType<ScrollControl>().Single();
                Check(detailText.Height > detailScroll.Height && detailScroll.MouseInputEnabled, "Long descriptions are scrollable");
                window.Apply(new RoyalContractBoardState { Enabled = false, ServerTime = now });
                Check(All(window).OfType<Button>().First(b => b.Name == "ContractAction").IsDisabled, "Paused/empty board has no action");
                window.Hide(); Check(window.IsHidden, "Closing preserves hidden state");
            }
        }
        languageProperty.SetValue(null, "en");
        var catalog = GameTextCatalog.FromJson(File.ReadAllText(Path.Combine(root, "Framework", "Intersect.Framework.Core", "RoyalContracts", "Text.json")));
        foreach (var (source, versions) in catalog.Entries)
            foreach (var language in GameTextCatalog.Languages) Check(versions.TryGetValue(language, out var translated) && GameTextTokens.Compatible(source, translated), "Locale coverage/placeholders: " + source + "/" + language);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
        System.Windows.Forms.Application.EnableVisualStyles();
        using (var editor = new FrmRoyalContracts(RoyalContractRules.Json(config)))
        {
            editor.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            editor.Location = new SD.Point(-20000, -20000); editor.ShowInTaskbar = false; editor.Opacity = 0;
            editor.Show(); System.Windows.Forms.Application.DoEvents();
            var split = editor.Controls.OfType<System.Windows.Forms.SplitContainer>().Single();
            Check(split.Panel1.Width is >= 240 and <= 350 && split.Panel2.Width >= 550, "Management form keeps room for both catalog and fields");
            using var bitmap = new SD.Bitmap(editor.Width, editor.Height);
            editor.DrawToBitmap(bitmap, new SD.Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(Path.Combine(output, "gestion-contrats-royaux.png"));
            Check(editor.Text == "Gestion des contrats royaux", "Native management form localized"); editor.Close();
        }
        Console.WriteLine($"Royal Contracts UI: {checks} checks passed; native Gwen layout rendered with GDI test fonts, plus native editor preview.");
    }
    private static IEnumerable<Control> All(Control parent)
    {
        foreach (var child in parent.Children) { yield return child; foreach (var next in All(child)) yield return next; }
    }
}

public class LogContextProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.Name == "get_Logger"
        ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
        : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}
