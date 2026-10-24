using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmCraftingTables
{
    private CheckBox _campfireCreator = null!;
    private bool _updatingCampfireCreator;
    private void InitializeCampfireCreator()
    {
        var language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        _campfireCreator = new LogiklikCheckBox
        {
            Name = "CampfireCreator", Text = QualityOfLifeText.Get("Use this table as the camp fire creator", language),
            Left = 8, Top = 76, Width = 234, Height = 48,
        };
        grpGeneral.Height += 104;
        grpGeneral.Controls.Add(_campfireCreator);
        grpGeneral.Controls.Add(new Label
        {
            Left = 8, Top = 126, Width = 234, Height = 50,
            Text = QualityOfLifeText.Get("Adds existing campfire recipes to this table. Other recipes are kept.", language),
        });
        grpCrafts.Top += 104;
        pnlContainer.AutoScroll = true;
        _campfireCreator.CheckedChanged += (_, _) =>
        {
            if (_updatingCampfireCreator || mEditorItem == null) return;
            ConfigureCampfireTable(mEditorItem, _campfireCreator.Checked);
            UpdateList();
        };
    }

    internal static void ConfigureCampfireTable(CraftingTableDescriptor table, bool enabled)
    {
        var recipes = CraftingRecipeDescriptor.Lookup.Values.OfType<CraftingRecipeDescriptor>()
            .Where(recipe => recipe.Campfire != null).ToArray();
        foreach (var recipe in recipes)
        {
            if (enabled && recipe.Campfire is { IsValid: true } && !table.Crafts.Contains(recipe.Id)) table.Crafts.Add(recipe.Id);
            else if (!enabled) table.Crafts.RemoveAll(id => id == recipe.Id);
        }
    }

    private void UpdateCampfireCreator()
    {
        _updatingCampfireCreator = true;
        try { _campfireCreator.Checked = mEditorItem?.Crafts.Any(id => CraftingRecipeDescriptor.Get(id)?.Campfire != null) == true; }
        finally { _updatingCampfireCreator = false; }
    }
}
