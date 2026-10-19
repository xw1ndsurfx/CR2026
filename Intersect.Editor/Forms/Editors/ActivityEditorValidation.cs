using Intersect.Framework.Core.Metalworking;
using Intersect.Framework.Core.Pvp;
using static Intersect.Editor.Localization.EditorSafetyText;

namespace Intersect.Editor.Forms.Editors;

internal static class ActivityEditorValidation
{
    internal static string? Metalworking(MetalworkingConfiguration config)
    {
        if (config.Recipes.Count > 200) return Get("Maximum {0} recipes.", 200);
        for (var row = 0; row < config.Recipes.Count; row++)
        {
            var recipe = config.Recipes[row];
            string Error(string detail) => Get("Recipe {0} ({1}): {2}", row + 1, recipe.Name, detail);
            if (string.IsNullOrWhiteSpace(recipe.Name) || recipe.Name.Length > 96) return Error(Get("Enter a name of 1 to {0} characters.", 96));
            if (recipe.Ingredients.Count > 8) return Error(Get("Maximum {0} distinct materials.", 8));
            for (var i = 0; i < recipe.Ingredients.Count; i++)
            {
                var ingredient = recipe.Ingredients[i];
                if (ingredient.ItemId == Guid.Empty) return Error(Get("Material row {0}: select an item.", i + 1));
                if (ingredient.Quantity is < 1 or > 1000) return Error(Get("Material row {0}: quantity must be 1 to 1000.", i + 1));
            }
            if (recipe.Ingredients.Select(i => i.ItemId).Distinct().Count() != recipe.Ingredients.Count)
                return Error(Get("Each material must appear once; combine its quantities."));
            if (recipe.Enabled && (recipe.OutputItemId == Guid.Empty || recipe.Ingredients.Count == 0))
                return Error(Get("An enabled recipe needs an output item and at least one material."));
            if (recipe.Id == Guid.Empty || config.Recipes.Count(r => r.Id == recipe.Id) > 1)
                return Error(Get("Duplicate or missing identifier. Remove this duplicate and add a new entry."));
            if (!recipe.IsValid) return Error(Get("Check quantity (1–100), level (1–1000), XP (0–5000), quality (1–100) and sound filenames."));
        }
        return null;
    }

    internal static string? Pvp(PvpConfiguration config)
    {
        if (config.Grades.Count != 10) return Get("Exactly ten PvP grades are required; found {0}.", config.Grades.Count);
        for (var row = 0; row < config.Grades.Count; row++)
        {
            var grade = config.Grades[row];
            if (string.IsNullOrWhiteSpace(grade.Name) || grade.Name.Length > 64)
                return Get("Grade {0}: enter a name of 1 to 64 characters.", row + 1);
            if (grade.Points < 0 || grade.Points > 10000000 || row == 0 && grade.Points != 0 || row > 0 && grade.Points <= config.Grades[row - 1].Points)
                return Get("Grade {0}: thresholds must start at zero and strictly increase (maximum 10000000).", row + 1);
        }
        if (config.Arenas.Count > 100) return Get("Maximum {0} arenas.", 100);
        for (var row = 0; row < config.Arenas.Count; row++)
        {
            var arena = config.Arenas[row];
            string Error(string detail) => Get("Arena {0} ({1}): {2}", row + 1, arena.Name, detail);
            if (string.IsNullOrWhiteSpace(arena.Name) || arena.Name.Length > 96) return Error(Get("Enter a name of 1 to {0} characters.", 96));
            if (arena.Enabled && arena.MapId == Guid.Empty) return Error(Get("Select an Arena-zone map for this enabled arena."));
            if (arena.RedX < 0 || arena.RedY < 0 || arena.BlueX < 0 || arena.BlueY < 0 ||
                arena.RedX >= Options.Instance.Map.MapWidth || arena.BlueX >= Options.Instance.Map.MapWidth ||
                arena.RedY >= Options.Instance.Map.MapHeight || arena.BlueY >= Options.Instance.Map.MapHeight)
                return Error(Get("Base coordinates must fit the map: X 0–{0}, Y 0–{1}.", Options.Instance.Map.MapWidth - 1, Options.Instance.Map.MapHeight - 1));
            if (Math.Abs(arena.RedX - arena.BlueX) + Math.Abs(arena.RedY - arena.BlueY) < 4)
                return Error(Get("Separate the bases by at least four tiles (X distance + Y distance)."));
            if (arena.Id == Guid.Empty || config.Arenas.Count(a => a.Id == arena.Id) > 1)
                return Error(Get("Duplicate or missing identifier. Remove this duplicate and add a new entry."));
            if (!arena.IsValid) return Error(Get("Check team size (1–8), minutes (1–60), score (1–100) and minimum level (1–10000)."));
        }
        return config.IsValid ? null : Get("Check ranked maps and the limits in Rules.");
    }

    internal static bool CommitGrid(DataGridView grid)
    {
        if (!grid.EndEdit()) return false;
        if (grid.DataSource != null) grid.BindingContext[grid.DataSource].EndCurrentEdit();
        return !grid.Rows.Cast<DataGridViewRow>().Any(row => !string.IsNullOrEmpty(row.ErrorText) ||
            row.Cells.Cast<DataGridViewCell>().Any(cell => !string.IsNullOrEmpty(cell.ErrorText)));
    }

    internal static void ReportGridError(DataGridView grid, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = Get("Select a listed value or enter a valid whole number.");
    }
}
