using System.Net;
using System.Text.RegularExpressions;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Framework.Input;
using Newtonsoft.Json.Linq;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// In-game reader for Corps Royaux announcements published through Logiklik News.
/// The public announcement feed is filtered client-side to the CR category.
/// </summary>
internal sealed class LogiklikNewsWindow : Window
{
    private const string FeedUrl = "https://logiklik.com/annonces_json.php";
    private const string RequiredCategory = "CR";

    private static readonly HttpClient s_httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12),
    };

    private readonly ListBox _newsList;
    private readonly Label _status;
    private readonly Label _detailTitle;
    private readonly Label _detailDate;
    private readonly ScrollControl _detailArea;
    private readonly RichLabel _detailLabel;
    private readonly Label _detailTemplate;
    private readonly Button _refreshButton;

    private bool _initialized;
    private bool _loading;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private List<NewsItem> _items = [];

    public LogiklikNewsWindow(Canvas parent) : base(parent, "Corps Royaux News", false, nameof(LogiklikNewsWindow))
    {
        DisableResizing();
        Alignment = [Alignments.Center];
        MinimumSize = new Point(820, 580);
        IsResizable = false;
        IsClosable = true;
        SetSize(820, 580);

        var header = new Label(this, "NewsHeader")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 14,
            Text = "Corps Royaux News",
            TextColorOverride = new Color(238, 211, 136, 255),
            TextAlign = Pos.Left | Pos.CenterV,
        };
        header.SetBounds(20, 14, 540, 28);

        var subHeader = new Label(this, "NewsSubHeader")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 9,
            Text = "Powered by Logiklik News • Category: CR",
            TextColorOverride = new Color(160, 174, 186, 255),
            TextAlign = Pos.Left | Pos.CenterV,
        };
        subHeader.SetBounds(20, 40, 540, 20);

        _refreshButton = new Button(this, "NewsRefresh")
        {
            Text = "Refresh",
            Font = Skin.DefaultFont,
            FontSize = 9,
        };
        _refreshButton.SetBounds(708, 22, 82, 26);
        _refreshButton.Clicked += RefreshButton_Clicked;

        _status = new Label(this, "NewsStatus")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(190, 198, 205, 255),
            TextAlign = Pos.Left | Pos.CenterV,
        };
        _status.SetBounds(20, 65, 770, 20);

        _newsList = new ListBox(this, "NewsList");
        _newsList.EnableScroll(false, true);
        _newsList.SetBounds(20, 92, 278, 440);

        _detailTitle = new Label(this, "NewsDetailTitle")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 13,
            TextColorOverride = Color.White,
            TextAlign = Pos.Left | Pos.CenterV,
        };
        _detailTitle.SetBounds(320, 92, 470, 42);

        _detailDate = new Label(this, "NewsDetailDate")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(238, 211, 136, 255),
            TextAlign = Pos.Left | Pos.CenterV,
        };
        _detailDate.SetBounds(320, 134, 470, 20);

        _detailArea = new ScrollControl(this, "NewsDetailArea");
        _detailArea.SetBounds(320, 162, 470, 370);

        _detailLabel = new RichLabel(_detailArea)
        {
            MouseInputEnabled = false,
        };
        _detailLabel.SetBounds(0, 0, 438, 360);

        _detailTemplate = new Label(null)
        {
            Font = Skin.DefaultFont,
            FontSize = 10,
            TextColor = new Color(225, 230, 234, 255),
            Width = 438,
        };

        SetEmptyDetail("Select a news item.");
    }

    public void ShowAndRefresh()
    {
        Show();

        var stale = DateTime.UtcNow - _lastRefreshUtc > TimeSpan.FromMinutes(5);
        if (_items.Count == 0 || stale)
        {
            _ = RefreshAsync();
        }
    }

    protected override void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
    }

    private void RefreshButton_Clicked(Base sender, MouseButtonState arguments)
    {
        _ = RefreshAsync(force: true);
    }

    private async Task RefreshAsync(bool force = false)
    {
        if (_loading)
        {
            return;
        }

        if (!force &&
            _items.Count > 0 &&
            DateTime.UtcNow - _lastRefreshUtc <= TimeSpan.FromMinutes(5))
        {
            return;
        }

        _loading = true;
        RunOnMainThread(
            () =>
            {
                _status.Text = "Loading Corps Royaux news...";
                _refreshButton.IsDisabled = true;
            }
        );

        try
        {
            var json = await s_httpClient.GetStringAsync(FeedUrl).ConfigureAwait(false);
            var items = ParseFeed(json);

            RunOnMainThread(
                () =>
                {
                    ApplyItems(items);
                    _lastRefreshUtc = DateTime.UtcNow;
                    _refreshButton.IsDisabled = false;
                    _loading = false;
                }
            );
        }
        catch (Exception exception)
        {
            RunOnMainThread(
                () =>
                {
                    _status.Text = "Unable to load Logiklik News. Try Refresh.";
                    _refreshButton.IsDisabled = false;
                    _loading = false;
                    SetEmptyDetail(exception.Message);
                }
            );
        }
    }

    private void ApplyItems(List<NewsItem> items)
    {
        _items = items;
        _newsList.RemoveAllRows();

        foreach (var item in _items)
        {
            var prefix = item.PublishedAt.HasValue
                ? item.PublishedAt.Value.ToLocalTime().ToString("yyyy-MM-dd") + "  "
                : string.Empty;

            var row = _newsList.AddRow(prefix + item.Title);
            row.UserData = item;
            row.Clicked += NewsRow_Clicked;
        }

        if (_items.Count == 0)
        {
            _status.Text = "No published news found in category CR.";
            SetEmptyDetail("No Corps Royaux news is currently available.");
            return;
        }

        _status.Text = $"{_items.Count} Corps Royaux news item{(_items.Count == 1 ? string.Empty : "s")}";
        SelectItem(_items[0]);
    }

    private void NewsRow_Clicked(Base sender, MouseButtonState arguments)
    {
        if (sender.UserData is NewsItem item)
        {
            SelectItem(item);
        }
    }

    private void SelectItem(NewsItem item)
    {
        _detailTitle.Text = item.Title;
        _detailDate.Text = item.PublishedAt.HasValue
            ? item.PublishedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "Corps Royaux";

        _detailLabel.ClearText();
        _detailTemplate.Width = Math.Max(100, _detailArea.Width - _detailArea.VerticalScrollBar.Width - 8);
        _detailLabel.Width = _detailTemplate.Width;
        _detailLabel.AddText(item.Summary, _detailTemplate);
        _detailLabel.SizeToChildren(false, true);
        _detailLabel.Invalidate();
    }

    private void SetEmptyDetail(string text)
    {
        _detailTitle.Text = string.Empty;
        _detailDate.Text = string.Empty;
        _detailLabel.ClearText();
        _detailLabel.AddText(text, _detailTemplate);
        _detailLabel.SizeToChildren(false, true);
    }

    private static List<NewsItem> ParseFeed(string json)
    {
        var root = JToken.Parse(json);
        IEnumerable<JToken> source = root.Type == JTokenType.Array
            ? root.Children()
            : FindProperty(root, "items", "annonces", "news")?.Children() ?? [];

        var items = new List<NewsItem>();
        foreach (var token in source)
        {
            if (token.Type != JTokenType.Object || !MatchesCrFilter(token))
            {
                continue;
            }

            var title = ReadString(token, "titre", "title", "Title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var summary = CleanHtml(
                ReadString(token, "description", "summary", "Summary", "contenu", "content")
            );

            var dateRaw = ReadString(token, "date_publication", "date", "Date", "published_at", "published");
            DateTimeOffset? publishedAt = null;
            if (DateTimeOffset.TryParse(dateRaw, out var parsedDate))
            {
                publishedAt = parsedDate;
            }

            items.Add(
                new NewsItem(
                    WebUtility.HtmlDecode(title).Trim(),
                    summary,
                    ReadString(token, "lien", "link", "Link"),
                    publishedAt
                )
            );
        }

        return items
            .OrderByDescending(item => item.PublishedAt ?? DateTimeOffset.MinValue)
            .Take(40)
            .ToList();
    }

    private static bool MatchesCrFilter(JToken token)
    {
        var filterToken = FindProperty(
            token,
            "categorie",
            "catégorie",
            "category",
            "categories",
            "category_name",
            "categorie_nom",
            "tags",
            "tenant",
            "tenant_code"
        );

        return TokenMatchesFilter(filterToken);
    }

    private static bool TokenMatchesFilter(JToken? token)
    {
        if (token == null)
        {
            return false;
        }

        if (token.Type == JTokenType.Array)
        {
            return token.Children().Any(TokenMatchesFilter);
        }

        if (token.Type == JTokenType.Object)
        {
            return token.Children<JProperty>().Any(property => TokenMatchesFilter(property.Value));
        }

        var value = token.ToString().Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value
            .Split([',', ';', '|', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part =>
                string.Equals(part, RequiredCategory, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part, "Corps Royaux", StringComparison.OrdinalIgnoreCase)
            );
    }

    private static string ReadString(JToken token, params string[] names) =>
        FindProperty(token, names)?.ToString() ?? string.Empty;

    private static JToken? FindProperty(JToken token, params string[] names)
    {
        if (token is not JObject obj)
        {
            return null;
        }

        foreach (var property in obj.Properties())
        {
            if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string CleanHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No description.";
        }

        var text = Regex.Replace(value, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</p\s*>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private sealed record NewsItem(
        string Title,
        string Summary,
        string Link,
        DateTimeOffset? PublishedAt
    );
}
