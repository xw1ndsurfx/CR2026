using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Networking;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class RoyalStylistWindow : Window
{
    private static readonly (string Name, Color Color)[] HairPalette =
    [
        ("Black", new Color(38, 30, 30)),
        ("Dark Brown", new Color(74, 48, 35)),
        ("Brown", new Color(116, 74, 46)),
        ("Auburn", new Color(145, 67, 39)),
        ("Blonde", new Color(210, 176, 98)),
        ("Silver", new Color(174, 176, 181)),
        ("White", Color.White),
        ("Blue", new Color(61, 104, 173)),
    ];

    private static readonly (string Name, Color Color)[] ClothesPalette =
    [
        ("Navy", new Color(42, 63, 104)),
        ("Burgundy", new Color(118, 48, 58)),
        ("Forest", new Color(55, 99, 63)),
        ("Brown", new Color(112, 75, 48)),
        ("Charcoal", new Color(58, 60, 66)),
        ("Gray", new Color(125, 127, 132)),
        ("Cream", new Color(222, 211, 178)),
        ("Gold", new Color(190, 151, 62)),
        ("White", Color.White),
    ];

    private readonly Label _description;
    private readonly Label _message;
    private readonly Label _premiumStatus;
    private readonly Label _priceStatus;

    private readonly ImagePanel _preview;
    private readonly Panel _previewPanel;
    private ImagePanel[]? _renderLayers;
    private int _previewDirection;

    private readonly LabeledComboBox _hair;
    private readonly LabeledComboBox _hairColor;
    private readonly LabeledComboBox _shirt;
    private readonly LabeledComboBox _shirtColor;
    private readonly LabeledComboBox _pants;
    private readonly LabeledComboBox _pantsColor;
    private readonly LabeledComboBox _boots;
    private readonly LabeledComboBox _bootsColor;

    private readonly Button _randomize;
    private readonly Button _reset;
    private readonly Button _apply;
    private readonly Button _cancel;

    private readonly Random _random = new();
    private RoyalStylistStatePacket? _state;
    private CharacterAppearance _originalAppearance = new();

    public RoyalStylistWindow(Canvas parent)
        : base(
            parent,
            "Royal Stylist",
            false,
            nameof(RoyalStylistWindow)
        )
    {
        SetSize(860, 640);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var titleFont =
            GameContentManager.Current.GetFont("sourcesansproblack") ??
            Skin.DefaultFont;
        var bodyFont =
            GameContentManager.Current.GetFont("sourcesanspro") ??
            Skin.DefaultFont;

        _description = new Label(this, "Description")
        {
            AutoSizeToContents = false,
            Font = bodyFont,
            FontSize = 10,
            TextColorOverride = Color.White,
        };
        _description.SetBounds(24, 42, 812, 40);

        _message = new Label(this, "Message")
        {
            AutoSizeToContents = false,
            Font = titleFont,
            FontSize = 9,
            TextColorOverride =
                new Color(a: 255, r: 220, g: 196, b: 135),
        };
        _message.SetBounds(24, 83, 812, 28);

        _premiumStatus = new Label(this, "PremiumStatus")
        {
            AutoSizeToContents = false,
            Font = titleFont,
            FontSize = 9,
            TextColorOverride = Color.White,
        };
        _premiumStatus.SetBounds(24, 111, 390, 26);

        _priceStatus = new Label(this, "PriceStatus")
        {
            AutoSizeToContents = false,
            Font = titleFont,
            FontSize = 9,
            TextAlign = Pos.Right | Pos.CenterV,
            TextColorOverride = Color.White,
        };
        _priceStatus.SetBounds(430, 111, 406, 26);

        _previewPanel = new Panel(this, "PreviewPanel")
        {
            ShouldDrawBackground = false,
        };
        _previewPanel.SetBounds(24, 145, 352, 410);

        _preview = new ImagePanel(
            _previewPanel,
            "CharacterPreview"
        )
        {
            MaintainAspectRatio = true,
            TextureFilename = "character_preview_background.png",
        };
        _preview.SetBounds(26, 12, 300, 330);

        AddDirectionButton("DOWN", 0, 10);
        AddDirectionButton("LEFT", 1, 92);
        AddDirectionButton("RIGHT", 2, 174);
        AddDirectionButton("UP", 3, 256);

        var x = 398;
        var y = 148;
        const int width = 438;
        const int rowHeight = 39;

        _hair = CreateCombo(
            "Hair",
            x,
            y,
            width
        );
        y += rowHeight;
        _hairColor = CreateCombo(
            "Hair Color",
            x,
            y,
            width
        );
        y += rowHeight;

        _shirt = CreateCombo(
            "Shirt",
            x,
            y,
            width
        );
        y += rowHeight;
        _shirtColor = CreateCombo(
            "Shirt Color",
            x,
            y,
            width
        );
        y += rowHeight;

        _pants = CreateCombo(
            "Pants",
            x,
            y,
            width
        );
        y += rowHeight;
        _pantsColor = CreateCombo(
            "Pants Color",
            x,
            y,
            width
        );
        y += rowHeight;

        _boots = CreateCombo(
            "Boots",
            x,
            y,
            width
        );
        y += rowHeight;
        _bootsColor = CreateCombo(
            "Boots Color",
            x,
            y,
            width
        );

        _randomize = new Button(this, "Randomize")
        {
            Text = "RANDOMIZE",
            Font = titleFont,
            FontSize = 9,
        };
        _randomize.SetBounds(398, 477, 208, 34);
        _randomize.Clicked += (_, _) =>
        {
            RandomizeSelection();
            UpdatePreview();
        };

        _reset = new Button(this, "Reset")
        {
            Text = "RESET",
            Font = titleFont,
            FontSize = 9,
        };
        _reset.SetBounds(628, 477, 208, 34);
        _reset.Clicked += (_, _) =>
        {
            LoadAppearance(_originalAppearance);
            UpdatePreview();
        };

        _apply = new Button(this, "Apply")
        {
            Text = "APPLY CHANGES",
            Font = titleFont,
            FontSize = 10,
        };
        _apply.SetBounds(398, 521, 438, 40);
        _apply.Clicked += (_, _) => ApplyChanges();

        _cancel = new Button(this, "Cancel")
        {
            Text = "CANCEL",
            Font = titleFont,
            FontSize = 9,
        };
        _cancel.SetBounds(24, 570, 812, 34);
        _cancel.Clicked += (_, _) =>
        {
            LoadAppearance(_originalAppearance);
            Hide();
        };

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(RoyalStylistStatePacket state)
    {
        _state = state;
        Title = string.IsNullOrWhiteSpace(state.StylistName)
            ? "Royal Stylist"
            : state.StylistName;

        _description.Text = state.Description ?? string.Empty;
        _message.Text = state.Message ?? string.Empty;

        _premiumStatus.Text = state.PremiumRequired
            ? state.PremiumActive
                ? "PREMIUM: ACTIVE"
                : "PREMIUM: REQUIRED"
            : "PREMIUM: NOT REQUIRED";

        _premiumStatus.TextColorOverride =
            !state.PremiumRequired || state.PremiumActive
                ? new Color(a: 255, r: 145, g: 230, b: 150)
                : new Color(a: 255, r: 225, g: 150, b: 135);

        _priceStatus.Text = state.Price <= 0
            ? "PRICE: FREE"
            : $"PRICE: {state.Price:N0} {state.CurrencyName}  |  " +
              $"BALANCE: {state.Balance:N0}";

        _originalAppearance =
            CloneAppearance(state.Appearance);

        PopulateStyleCombo(
            _hair,
            state.HairStyles,
            state.Appearance.HairStyle,
            CharacterAppearance.HairPrefix,
            state.AllowNoHair
        );
        PopulateColorCombo(
            _hairColor,
            HairPalette,
            state.Appearance.HairColor
        );

        PopulateStyleCombo(
            _shirt,
            state.ShirtStyles,
            state.Appearance.ShirtStyle,
            CharacterAppearance.ShirtPrefix,
            state.AllowNoShirt
        );
        PopulateColorCombo(
            _shirtColor,
            ClothesPalette,
            state.Appearance.ShirtColor
        );

        PopulateStyleCombo(
            _pants,
            state.PantsStyles,
            state.Appearance.PantsStyle,
            CharacterAppearance.PantsPrefix,
            state.AllowNoPants
        );
        PopulateColorCombo(
            _pantsColor,
            ClothesPalette,
            state.Appearance.PantsColor
        );

        PopulateStyleCombo(
            _boots,
            state.BootsStyles,
            state.Appearance.BootsStyle,
            CharacterAppearance.BootsPrefix,
            state.AllowNoBoots
        );
        PopulateColorCombo(
            _bootsColor,
            ClothesPalette,
            state.Appearance.BootsColor
        );

        _apply.IsDisabled = !state.CanApply ||
                            state.StylistId == Guid.Empty;

        _apply.Text = state.Price <= 0
            ? "APPLY CHANGES - FREE"
            : $"APPLY CHANGES - {state.Price:N0} " +
              state.CurrencyName;

        EnsureRenderLayers();
        UpdatePreview();
    }

    private LabeledComboBox CreateCombo(
        string label,
        int x,
        int y,
        int width
    )
    {
        var combo = new LabeledComboBox(
            this,
            label.Replace(" ", string.Empty)
        )
        {
            AutoSizeToContents = false,
            Label = label,
            Font =
                GameContentManager.Current.GetFont(
                    "sourcesansproblack"
                ) ?? Skin.DefaultFont,
            FontSize = 9,
        };
        combo.SetBounds(x, y, width, 31);
        combo.ItemSelected += (_, _) => UpdatePreview();
        return combo;
    }

    private void AddDirectionButton(
        string text,
        int direction,
        int x
    )
    {
        var button = new Button(
            _previewPanel,
            "Direction" + direction
        )
        {
            Text = text,
            Font =
                GameContentManager.Current.GetFont(
                    "sourcesansproblack"
                ) ?? Skin.DefaultFont,
            FontSize = 8,
        };
        button.SetBounds(x, 356, 76, 30);
        button.Clicked += (_, _) =>
        {
            _previewDirection = direction;
            UpdatePreview();
        };
    }

    private void EnsureRenderLayers()
    {
        var required = Options.Instance.Equipment.Paperdoll.Directions
            .Max(direction => direction.Count);

        if (_renderLayers is { Length: var length } &&
            length == required)
        {
            return;
        }

        if (_renderLayers != null)
        {
            foreach (var layer in _renderLayers)
            {
                layer.Dispose();
            }
        }

        _renderLayers = new ImagePanel[required];
        for (var i = 0; i < required; i++)
        {
            _renderLayers[i] = new ImagePanel(_preview)
            {
                Alignment = [Alignments.Center],
                RestrictToParent = false,
            };
        }
    }

    private void UpdatePreview()
    {
        if (_renderLayers == null ||
            Globals.Me == null)
        {
            return;
        }

        var paperdollOrder =
            Options.Instance.Equipment.Paperdoll
                .Directions[_previewDirection];

        var appearance = CurrentAppearance();

        for (var layerIndex = 0;
             layerIndex < _renderLayers.Length;
             layerIndex++)
        {
            var container = _renderLayers[layerIndex];
            container.Texture = default;
            container.RenderColor = Color.White;

            if (layerIndex >= paperdollOrder.Count)
            {
                container.Hide();
                continue;
            }

            var layerType = paperdollOrder[layerIndex];
            if (string.Equals(
                    "Player",
                    layerType,
                    StringComparison.Ordinal
                ))
            {
                container.Texture =
                    Globals.ContentManager.GetTexture(
                        TextureType.Entity,
                        Globals.Me.Sprite
                    );
                container.RenderColor = Globals.Me.Color;
            }
            else if (appearance.TryGetLayer(
                         layerType,
                         out var style,
                         out var color
                     ))
            {
                container.Texture =
                    Globals.ContentManager.GetTexture(
                        TextureType.Paperdoll,
                        style
                    );
                container.RenderColor = color;
            }

            var texture = container.Texture;
            if (texture == default)
            {
                container.Hide();
                continue;
            }

            var textureWidth =
                texture.Width /
                Options.Instance.Sprites.NormalFrames;
            var textureHeight =
                texture.Height /
                Options.Instance.Sprites.Directions;

            if (textureWidth <= 0 || textureHeight <= 0)
            {
                container.Hide();
                continue;
            }

            container.SetTextureRect(
                0,
                _previewDirection * textureHeight,
                textureWidth,
                textureHeight
            );
            _ = container.SetSize(
                textureWidth,
                textureHeight
            );
            container.SetPosition(
                (_preview.Width - textureWidth) / 2,
                (_preview.Height - textureHeight) / 2
            );
            container.Show();
        }
    }

    private static void PopulateStyleCombo(
        LabeledComboBox combo,
        IEnumerable<string>? offered,
        string? current,
        string prefix,
        bool allowNone
    )
    {
        combo.ClearItems();

        if (allowNone || string.IsNullOrWhiteSpace(current))
        {
            combo.AddItem("None", userData: string.Empty);
        }

        var styles = (offered ?? [])
            .Where(style =>
                CharacterAppearance.IsCatalogStyle(
                    style,
                    prefix
                ) &&
                !string.IsNullOrWhiteSpace(style))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                style => style,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();

        if (!string.IsNullOrWhiteSpace(current) &&
            !styles.Contains(
                current,
                StringComparer.OrdinalIgnoreCase
            ))
        {
            styles.Insert(0, current);
        }

        foreach (var style in styles)
        {
            var display = FriendlyStyle(style, prefix);
            if (string.Equals(
                    style,
                    current,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                !(offered ?? []).Contains(
                    style,
                    StringComparer.OrdinalIgnoreCase
                ))
            {
                display += " (Current)";
            }

            combo.AddItem(display, userData: style);
        }

        if (!combo.SelectByUserData(current ?? string.Empty))
        {
            combo.SelectByUserData(string.Empty);
        }
    }

    private static void PopulateColorCombo(
        LabeledComboBox combo,
        IReadOnlyList<(string Name, Color Color)> palette,
        Color? current
    )
    {
        combo.ClearItems();

        var value = current ?? Color.White;
        var matched = false;

        foreach (var entry in palette)
        {
            combo.AddItem(
                entry.Name,
                userData: new Color(entry.Color)
            );

            if (entry.Color == value)
            {
                matched = true;
            }
        }

        if (!matched)
        {
            combo.AddItem(
                $"Current ({value.R},{value.G},{value.B})",
                userData: new Color(value)
            );
        }

        combo.SelectByUserData(new Color(value));
    }

    private static string FriendlyStyle(
        string style,
        string prefix
    )
    {
        var value =
            Path.GetFileNameWithoutExtension(style);

        if (value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            value = value[prefix.Length..];
        }

        value = value
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return style;
        }

        return string.Join(
            " ",
            value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(part =>
                    char.ToUpperInvariant(part[0]) +
                    part[1..])
        );
    }

    private CharacterAppearance CurrentAppearance()
    {
        return new CharacterAppearance
        {
            HairStyle = SelectedStyle(_hair),
            HairColor = SelectedColor(_hairColor),
            ShirtStyle = SelectedStyle(_shirt),
            ShirtColor = SelectedColor(_shirtColor),
            PantsStyle = SelectedStyle(_pants),
            PantsColor = SelectedColor(_pantsColor),
            BootsStyle = SelectedStyle(_boots),
            BootsColor = SelectedColor(_bootsColor),
        }.SanitizedCopy();
    }

    private static string SelectedStyle(
        LabeledComboBox combo
    ) =>
        combo.SelectedItem?.UserData as string ??
        string.Empty;

    private static Color SelectedColor(
        LabeledComboBox combo
    ) =>
        combo.SelectedItem?.UserData is Color color
            ? new Color(color)
            : Color.White;

    private void LoadAppearance(CharacterAppearance appearance)
    {
        _hair.SelectByUserData(
            appearance.HairStyle ?? string.Empty
        );
        SelectOrAddColor(
            _hairColor,
            appearance.HairColor
        );

        _shirt.SelectByUserData(
            appearance.ShirtStyle ?? string.Empty
        );
        SelectOrAddColor(
            _shirtColor,
            appearance.ShirtColor
        );

        _pants.SelectByUserData(
            appearance.PantsStyle ?? string.Empty
        );
        SelectOrAddColor(
            _pantsColor,
            appearance.PantsColor
        );

        _boots.SelectByUserData(
            appearance.BootsStyle ?? string.Empty
        );
        SelectOrAddColor(
            _bootsColor,
            appearance.BootsColor
        );
    }

    private static void SelectOrAddColor(
        LabeledComboBox combo,
        Color? color
    )
    {
        var value = color ?? Color.White;
        if (!combo.SelectByUserData(new Color(value)))
        {
            combo.AddItem(
                $"Current ({value.R},{value.G},{value.B})",
                userData: new Color(value)
            );
            combo.SelectByUserData(new Color(value));
        }
    }

    private void RandomizeSelection()
    {
        if (_state == null)
        {
            return;
        }

        RandomizeStyle(
            _hair,
            _state.HairStyles,
            _state.AllowNoHair
        );
        RandomizeStyle(
            _shirt,
            _state.ShirtStyles,
            _state.AllowNoShirt
        );
        RandomizeStyle(
            _pants,
            _state.PantsStyles,
            _state.AllowNoPants
        );
        RandomizeStyle(
            _boots,
            _state.BootsStyles,
            _state.AllowNoBoots
        );

        RandomizeColor(_hairColor, HairPalette);
        RandomizeColor(_shirtColor, ClothesPalette);
        RandomizeColor(_pantsColor, ClothesPalette);
        RandomizeColor(_bootsColor, ClothesPalette);
    }

    private void RandomizeStyle(
        LabeledComboBox combo,
        IReadOnlyList<string>? values,
        bool allowNone
    )
    {
        var choices = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (allowNone)
        {
            choices.Add(string.Empty);
        }

        if (choices.Count > 0)
        {
            combo.SelectByUserData(
                choices[_random.Next(choices.Count)]
            );
        }
    }

    private void RandomizeColor(
        LabeledComboBox combo,
        IReadOnlyList<(string Name, Color Color)> palette
    )
    {
        if (palette.Count == 0)
        {
            return;
        }

        combo.SelectByUserData(
            new Color(
                palette[_random.Next(palette.Count)].Color
            )
        );
    }

    private void ApplyChanges()
    {
        if (_state == null ||
            _state.StylistId == Guid.Empty ||
            !_state.CanApply)
        {
            return;
        }

        _apply.IsDisabled = true;
        _apply.Text = "APPLYING...";

        PacketSender.SendApplyRoyalStylistAppearance(
            _state.StylistId,
            CurrentAppearance()
        );
    }

    private static CharacterAppearance CloneAppearance(
        CharacterAppearance? appearance
    )
    {
        appearance ??= new CharacterAppearance();

        return new CharacterAppearance
        {
            HairStyle = appearance.HairStyle,
            HairColor = new Color(
                appearance.HairColor ?? Color.White
            ),
            ShirtStyle = appearance.ShirtStyle,
            ShirtColor = new Color(
                appearance.ShirtColor ?? Color.White
            ),
            PantsStyle = appearance.PantsStyle,
            PantsColor = new Color(
                appearance.PantsColor ?? Color.White
            ),
            BootsStyle = appearance.BootsStyle,
            BootsColor = new Color(
                appearance.BootsColor ?? Color.White
            ),
        };
    }
}
