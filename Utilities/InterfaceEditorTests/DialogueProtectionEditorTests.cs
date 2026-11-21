using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;

internal static class DialogueProtectionEditorTests
{
    internal static void Run()
    {
        var previous = CultureInfo.CurrentUICulture;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var expected = new Dictionary<string, string>
        {
            ["en"] = "Protect player during dialogue",
            ["fr"] = "Protéger le joueur pendant les dialogues",
            ["es"] = "Proteger al jugador durante los diálogos",
            ["de"] = "Protect player during dialogue"
        };
        try
        {
            foreach (var (language, caption) in expected)
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                using var editor = new FrmEvent(null);
                var tree = (TreeView)typeof(FrmEvent).GetField("lstCommands", flags)!.GetValue(editor)!;
                var node = tree.Nodes.Find("protectdialogue", true).Single();
                Check(node.Text == caption && node.Tag is int value && value == (int)EventCommandType.ProtectDialogue,
                    "Localized command must be available under Dialogue: " + language);
                var roots = (List<TreeNode>)typeof(FrmEvent).GetField("_commandRoots", flags)!.GetValue(editor)!;
                Check(roots.SelectMany(root => root.Nodes.Find("protectdialogue", true)).Any(), "Searchable command snapshot includes protection.");
                var print = typeof(CommandPrinter).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Single(method => method.Name == "GetCommandText" && method.GetParameters()[0].ParameterType == typeof(ProtectDialogueCommand));
                Check((string)print.Invoke(null, [new ProtectDialogueCommand(), null])! == caption, "Event list caption is localized.");
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; }
        Console.WriteLine("PASS dialogue protection editor: EN/FR/ES, English fallback, command tree, search and command listing");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
