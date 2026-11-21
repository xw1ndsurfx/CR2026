using Intersect.Editor.Core;
using Intersect.Editor.Forms.Styling;
using Intersect.IO.Files;

internal static class CrashStabilityTests
{
    internal static void Run()
    {
        var handler = (Intersect.Editor.Networking.PacketHandler)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Intersect.Editor.Networking.PacketHandler));
        var deletion = new Intersect.Network.Packets.Server.GameObjectPacket(Guid.NewGuid(),
            Intersect.Enums.GameObjectType.Npc, "", true, false);
        handler.HandlePacket(null!, deletion);
        handler.HandlePacket(null!, deletion);
        using (var grid = new PropertyGrid())
        {
            grid.CreateControl();
            var internals = grid.Controls.Cast<Control>().Where(c =>
                c.GetType().Namespace?.StartsWith("System.Windows.Forms.PropertyGridInternal") == true).ToArray();
            Check(internals.Length > 0, "Real PropertyGrid implementation controls found");
            foreach (var control in internals)
            {
                var color = control.BackColor;
                var handle = control.Handle;
                var recreated = 0;
                control.HandleCreated += (_, _) => recreated++;
                EditorTheme.Apply(control);
                Check(control.BackColor == color && control.Handle == handle && recreated == 0,
                    "The theme must leave PropertyGrid implementation controls alone");
            }
        }
        using (var window = new Form { ShowInTaskbar = false })
        {
            EditorWindows.Show(window, window);
            Check(window.Visible && window.Owner == null, "Showing a window cannot make it its own owner");
            using var child = new Form { Owner = window, ShowInTaskbar = false };
            window.Hide();
            EditorWindows.Show(window, child);
            Check(window.Visible && window.Owner == null, "Showing a parent cannot reverse child ownership");
            window.Close();
        }
        var folder = Path.Combine(Path.GetTempPath(), "Intersect-stability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "sqlite3.dll");
            byte[] bytes = [1, 2, 3, 4];
            EmbeddedLibrary.EnsureExtracted(path, new MemoryStream(bytes));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                EmbeddedLibrary.EnsureExtracted(path, new MemoryStream(bytes));
                Check(locked.Length == bytes.Length, "A matching locked DLL can be reused");
            }
            EmbeddedLibrary.EnsureExtracted(path, new MemoryStream([9]));
            Check(File.ReadAllBytes(path).SequenceEqual(new byte[] { 9 }), "Replacement truncates old native library");
            Check(Directory.GetFiles(folder).Length == 1, "No extraction staging files left behind");
        }
        finally { Directory.Delete(folder, true); }

        var name = "stability-" + Guid.NewGuid().ToString("N") + ".xnb";
        var extracted = FileSystemHelper.WriteToTemporaryFolder(name, new MemoryStream([1, 2, 3, 4]));
        try
        {
            var again = FileSystemHelper.WriteToTemporaryFolder(name, new MemoryStream([9]));
            Check(extracted == again && File.ReadAllBytes(again).SequenceEqual(new byte[] { 9 }),
                "Repeated shader extraction truncates old content");
            Check(Path.GetDirectoryName(extracted)!.Contains(Environment.ProcessId + "-"),
                "Shader extraction is isolated per process and session");
        }
        finally { File.Delete(extracted); }
        Console.WriteLine("PASS crash stability: PropertyGrid, window ownership, locked SQLite, repeated NPC deletion and shader extraction");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
