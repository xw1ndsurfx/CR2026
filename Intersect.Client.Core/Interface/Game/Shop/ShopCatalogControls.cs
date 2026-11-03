using System.Globalization;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;

namespace Intersect.Client.Interface.Game.Shop;

/// <summary>Shared browsing controls; sorting preserves the server's offer identifiers.</summary>
internal sealed class ShopCatalogControls
{
    private readonly TextBox _search;
    private readonly ComboBox _sort, _filter;
    private bool _ready;

    internal ShopCatalogControls(Base parent, Action changed)
    {
        _search = new TextBox(parent, "CatalogSearch")
        {
            Font = parent.Skin.DefaultFont, FontSize = 11, MaximumLength = 64,
            PlaceholderText = ClientLanguages.Text("Search products..."),
        };
        _search.SetBounds(24, 12, 352, 30);
        _sort = new ComboBox(parent, "CatalogSort") { Font = parent.Skin.DefaultFont, FontSize = 11 };
        _sort.SetBounds(388, 12, 180, 30);
        foreach (var key in new[] { "New", "Old", "Level", "Price", "Price: high to low", "Name" })
            _sort.AddItem(ClientLanguages.Text(key), userData: key);
        _filter = new ComboBox(parent, "CatalogFilter") { Font = parent.Skin.DefaultFont, FontSize = 11 };
        _filter.SetBounds(580, 12, 176, 30);
        foreach (var key in new[] { "All products", "Available", "Owned", "Not owned" })
            _filter.AddItem(ClientLanguages.Text(key), userData: key);
        _sort.SelectByUserData("New");
        _filter.SelectByUserData("All products");
        _search.TextChanged += (_, _) => { if (_ready) changed(); };
        _sort.ItemSelected += (_, _) => { if (_ready) changed(); };
        _filter.ItemSelected += (_, _) => { if (_ready) changed(); };
        Interface.FocusComponents.Add(_search);
        parent.Disposed += (_, _) => Interface.FocusComponents.Remove(_search);
        _ready = true;
    }

    internal IEnumerable<T> Browse<T>(IEnumerable<T> source, Func<T, string> name, Func<T, int> level,
        Func<T, int> price, Func<T, bool> available, Func<T, bool> owned)
    {
        var query = _search.Text?.Trim() ?? "";
        var entries = source.Select((entry, index) => (Entry: entry, Index: index)).Where(pair =>
        {
            var entry = pair.Entry;
            var matchesName = query.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                ClientLanguages.ContentText(name(entry)), query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
            return matchesName && ((_filter.SelectedItem?.UserData as string) switch
            {
                "Available" => available(entry), "Owned" => owned(entry), "Not owned" => !owned(entry), _ => true,
            });
        });
        // Offers have no creation timestamp. New/Old use their authored catalog order.
        entries = (_sort.SelectedItem?.UserData as string) switch
        {
            "Old" => entries.OrderBy(e => e.Index),
            "Level" => entries.OrderBy(e => level(e.Entry)).ThenBy(e => e.Index),
            "Price" => entries.OrderBy(e => price(e.Entry)).ThenBy(e => e.Index),
            "Price: high to low" => entries.OrderByDescending(e => price(e.Entry)).ThenBy(e => e.Index),
            "Name" => entries.OrderBy(e => ClientLanguages.ContentText(name(e.Entry)), StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Index),
            _ => entries.OrderByDescending(e => e.Index),
        };
        return entries.Select(e => e.Entry);
    }
}
