using System.ComponentModel;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.Housing;
using Newtonsoft.Json;

internal static class HousingPickerTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run()
    {
        foreach (var existing in new[] { false, true })
        foreach (var columnName in new[] { "ChooseGraphic", "ChooseTileset" })
        {
            var config = new HousingConfiguration();
            if (existing) config.Catalog.Add(new FurnitureDefinition { Name = "Existing bed", Tileset = "bed.png" });
            var calls = 0; FurnitureDefinition? selected = null;
            using var form = new FrmHousingConfiguration(JsonConvert.SerializeObject(config), furniture => { calls++; selected = furniture; furniture.Tileset = "selected.png"; });
            var grid = (DataGridView)typeof(FrmHousingConfiguration).GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            _ = form.Handle; _ = grid.Handle; form.PerformLayout();
            var list = (BindingList<FurnitureDefinition>)grid.DataSource!;
            var click = typeof(DataGridView).GetMethod("OnCellContentClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Click(int row)
            {
                grid.CurrentCell = grid.Rows[row].Cells[columnName];
                click.Invoke(grid, [new DataGridViewCellEventArgs(grid.Columns[columnName].Index, row)]);
            }
            var initial = list.Count;
            Click(grid.NewRowIndex);
            Check(calls == 0, "Picker must not open inside the grid button callback");
            Application.DoEvents();
            Check(calls == 1 && list.Count == initial + 1 && selected != null && list.Contains(selected), $"A blank row creates exactly one bound furniture before opening the selector: existing={existing}, column={columnName}, calls={calls}, count={list.Count}, initial={initial}");
            Check(selected.Tileset == "selected.png", "Selection updates the actual catalog record");
            var created = selected; var id = selected.Id;
            Click(list.IndexOf(created)); Application.DoEvents();
            Check(calls == 2 && ReferenceEquals(selected, created) && selected.Id == id && list.Count == initial + 1, "Reopening an existing row preserves its record and ID");
            // A double activation is coalesced while the modal open is queued.
            Click(list.IndexOf(created)); Click(list.IndexOf(created)); Application.DoEvents();
            Check(calls == 3 && list.Count == initial + 1, "Repeated clicks do not duplicate records or dialogs");
            if (existing) Check(list[0].Name == "Existing bed" && list[0].Tileset == "bed.png", "Other furniture is preserved");
        }
        System.Console.WriteLine("PASS Housing visual picker: deferred real grid activation, empty/populated catalogs, new/existing rows, stable IDs and duplicate-click handling");
    }
}
