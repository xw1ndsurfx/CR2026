using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.General;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Framework.Reflection;
using Intersect.GameObjects;
using Intersect.Utilities;
using Microsoft.Extensions.Logging;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Intersect.Client.Interface.Menu;

public partial class CharacterCreationWindow : Window
{
    private readonly MainMenu _mainMenu;
    private readonly SelectCharacterWindow _selectCharacterWindow;

    private readonly IFont? _defaultFont;

    private readonly Panel _previewPanel;
    private readonly Panel _previewContainer;
    private readonly Panel _propertiesPanel;
    private readonly Panel _buttonsPanel;

    private readonly TextBox _nameInput;

    private readonly LabeledComboBox _classCombobox;

    private readonly LabeledComboBox _hairCombobox;
    private readonly LabeledComboBox _hairColorCombobox;
    private readonly LabeledComboBox _shirtCombobox;
    private readonly LabeledComboBox _shirtColorCombobox;
    private readonly LabeledComboBox _pantsCombobox;
    private readonly LabeledComboBox _pantsColorCombobox;
    private readonly LabeledComboBox _bootsCombobox;
    private readonly LabeledComboBox _bootsColorCombobox;
    private readonly Button _randomizeAppearanceButton;
    private readonly Panel _directionPanel;

    private readonly Panel _genderInputPanel;
    private readonly LabeledCheckBox _genderMaleCheckbox;
    private readonly LabeledCheckBox _genderFemaleCheckbox;

    private readonly ImagePanel _preview;
    private ImagePanel[]? _renderLayers;
    private readonly Button _nextSpriteButton;
    private readonly Button _prevSpriteButton;

    // Buttons
    private readonly Button _createButton;

    private int _displaySpriteIndex = -1;
    private int _previewDirection;
    private readonly Random _random = new();
    private readonly List<KeyValuePair<int, ClassSprite>> _femaleSprites = [];
    private readonly List<KeyValuePair<int, ClassSprite>> _maleSprites = [];
    private Button _backButton;


    public CharacterCreationWindow(Canvas parent, MainMenu mainMenu, SelectCharacterWindow selectCharacterWindow) :
        base(parent, title: Strings.CharacterCreation.Title, modal: false, name: nameof(CharacterCreationWindow))
    {
        _mainMenu = mainMenu;
        _selectCharacterWindow = selectCharacterWindow;

        _defaultFont = GameContentManager.Current.GetFont(name: "sourcesansproblack");

        Alignment = [Alignments.Center];
        MinimumSize = new Point(x: 860, y: 560);
        IsClosable = false;
        IsResizable = false;
        InnerPanelPadding = new Padding(8);
        Titlebar.MouseInputEnabled = false;
        TitleLabel.FontSize = 14;
        TitleLabel.TextColorOverride = Color.White;

        _buttonsPanel = new Panel(this, name: nameof(_buttonsPanel))
        {
            Dock = Pos.Bottom,
            Margin = new Margin(0, 8, 0, 0),
            ShouldDrawBackground = false,
        };

        _createButton = new Button(_buttonsPanel, name: nameof(_createButton))
        {
            Alignment = [Alignments.Left],
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(120, 24),
            Text = Strings.CharacterCreation.Create,
        };
        _createButton.Clicked += CreateButton_Clicked;

        _backButton = new Button(_buttonsPanel, name: nameof(_backButton))
        {
            Alignment = [Alignments.Right],
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(120, 24),
            Text = Strings.CharacterCreation.Back,
        };
        _backButton.Clicked += BackButton_Clicked;

        _propertiesPanel = new Panel(this, name: nameof(_propertiesPanel))
        {
            Dock = Pos.Right,
            DockChildSpacing = new Padding(8),
            Margin = new Margin(16, 0, 0, 0),
            ShouldDrawBackground = false,
        };

        _previewPanel = new Panel(this, name: nameof(_previewPanel))
        {
            Dock = Pos.Fill,
            ShouldDrawBackground = false,
        };

        _nameInput = new TextBox(_propertiesPanel, name: nameof(_nameInput))
        {
            Dock = Pos.Top,
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(240, 0),
            PlaceholderText = Strings.CharacterCreation.Name,
        };
        _nameInput.SubmitPressed += (sender, e) => TryCreateCharacter();

        _classCombobox = new LabeledComboBox(_propertiesPanel, name: nameof(_classCombobox))
        {
            AutoSizeToContents = false,
            Dock = Pos.Top,
            Font = _defaultFont,
            FontSize = 12,
            Label = Strings.CharacterCreation.Class,
        };
        _classCombobox.ItemSelected += classCombobox_ItemSelected;

        _hairCombobox = CreateAppearanceCombo(nameof(_hairCombobox), "Hair");
        _hairColorCombobox = CreateAppearanceCombo(nameof(_hairColorCombobox), "Hair Color");
        _shirtCombobox = CreateAppearanceCombo(nameof(_shirtCombobox), "Shirt");
        _shirtColorCombobox = CreateAppearanceCombo(nameof(_shirtColorCombobox), "Shirt Color");
        _pantsCombobox = CreateAppearanceCombo(nameof(_pantsCombobox), "Pants");
        _pantsColorCombobox = CreateAppearanceCombo(nameof(_pantsColorCombobox), "Pants Color");
        _bootsCombobox = CreateAppearanceCombo(nameof(_bootsCombobox), "Boots");
        _bootsColorCombobox = CreateAppearanceCombo(nameof(_bootsColorCombobox), "Boots Color");

        _randomizeAppearanceButton = new Button(_propertiesPanel, name: nameof(_randomizeAppearanceButton))
        {
            Dock = Pos.Top,
            Font = _defaultFont,
            FontSize = 11,
            MinimumSize = new Point(240, 26),
            Text = "Randomize Appearance",
        };
        _randomizeAppearanceButton.Clicked += (_, _) => RandomizeAppearance();

        _genderInputPanel = new Panel(_propertiesPanel, name: nameof(_genderInputPanel))
        {
            Dock = Pos.Top,
            ShouldDrawBackground = false,
        };

        // Male Checkbox
        _genderMaleCheckbox = new LabeledCheckBox(_genderInputPanel, name: nameof(_genderMaleCheckbox))
        {
            AutoSizeToContents = true,
            Dock = Pos.Left | Pos.CenterV,
            Font = _defaultFont,
            FontSize = 12,
            IsChecked = true,
            Text = Strings.CharacterCreation.Male,
        };
        _genderMaleCheckbox.Checked += MaleCheckboxGenderChecked;
        _genderMaleCheckbox.Unchecked += FemaleCheckboxGenderChecked;

        _genderFemaleCheckbox = new LabeledCheckBox(_genderInputPanel, name: nameof(_genderFemaleCheckbox))
        {
            AutoSizeToContents = true,
            Dock = Pos.Right | Pos.CenterV,
            Font = _defaultFont,
            FontSize = 12,
            Text = Strings.CharacterCreation.Female,
        };
        _genderFemaleCheckbox.Checked += FemaleCheckboxGenderChecked;
        _genderFemaleCheckbox.Unchecked += MaleCheckboxGenderChecked;

        _previewContainer = new Panel(_previewPanel, name: nameof(_previewContainer))
        {
            Dock = Pos.Fill,
            ShouldDrawBackground = false,
        };

        _prevSpriteButton = new Button(_previewPanel, name: nameof(_prevSpriteButton), disableText: true)
        {
            Dock = Pos.Left | Pos.CenterV,
            MinimumSize = new Point(30, 35),
            MaximumSize = new Point(30, 35),
        };
        _prevSpriteButton.Clicked += _prevSpriteButton_Clicked;
        _prevSpriteButton.SetStateTexture(ComponentState.Normal, "button.arrow_left.normal.png");
        _prevSpriteButton.SetStateTexture(ComponentState.Disabled, "button.arrow_left.disabled.png");
        _prevSpriteButton.SetStateTexture(ComponentState.Hovered, "button.arrow_left.hovered.png");
        _prevSpriteButton.SetStateTexture(ComponentState.Active, "button.arrow_left.active.png");

        _nextSpriteButton = new Button(_previewPanel, name: nameof(_nextSpriteButton), disableText: true)
        {
            Dock = Pos.Right | Pos.CenterV,
            MinimumSize = new Point(30, 35),
            MaximumSize = new Point(30, 35),
        };
        _nextSpriteButton.Clicked += _nextSpriteButton_Clicked;
        _nextSpriteButton.SetStateTexture(ComponentState.Normal, "button.arrow_right.normal.png");
        _nextSpriteButton.SetStateTexture(ComponentState.Disabled, "button.arrow_right.disabled.png");
        _nextSpriteButton.SetStateTexture(ComponentState.Hovered, "button.arrow_right.hovered.png");
        _nextSpriteButton.SetStateTexture(ComponentState.Active, "button.arrow_right.active.png");

        _preview = new ImagePanel(_previewContainer, name: nameof(_preview))
        {
            Alignment = [Alignments.Center],
            MaintainAspectRatio = true,
            TextureFilename = "character_preview_background.png",
        };

        _directionPanel = new Panel(_previewPanel, name: nameof(_directionPanel))
        {
            Dock = Pos.Bottom,
            DisplayMode = DisplayMode.FlowStartToEnd,
            DockChildSpacing = new Padding(6),
            Margin = new Margin(38, 0, 38, 0),
            ShouldDrawBackground = false,
        };

        AddDirectionButton("↓ Down", 0);
        AddDirectionButton("← Left", 1);
        AddDirectionButton("→ Right", 2);
        AddDirectionButton("↑ Up", 3);
        _directionPanel.SizeToChildren(recursive: true);

        _buttonsPanel.SizeToChildren(recursive: true);
        _propertiesPanel.SizeToChildren(recursive: true);
    }

    protected override void EnsureInitialized()
    {
        SizeToChildren(recursive: true);

        LoadJsonUi(GameContentManager.UI.Menu, Graphics.Renderer?.GetResolutionString());

        _classCombobox.ClearItems();

        var classDescriptors = ClassDescriptor.Lookup.Values.OfType<ClassDescriptor>()
            .Where(classDescriptor => !classDescriptor.Locked)
            .ToArray();

        foreach (var classDescriptor in classDescriptors)
        {
            _ = _classCombobox.AddItem(classDescriptor.Name, userData: classDescriptor);
        }

        ApplicationContext.Context.Value?.Logger.LogDebug(
            "Added {ClassCount} classes to the {WindowType}",
            classDescriptors.Length,
            typeof(CharacterCreationWindow).GetName(qualified: true)
        );

        PopulateAppearanceControls();
        LoadClass();
        UpdateDisplay();
    }

    public void Update()
    {
        if (!Networking.Network.IsConnected)
        {
            Hide();
            _mainMenu.Show();
            return;
        }

        // Re-Enable our buttons if we're not waiting for the server anymore with it disabled.
        if (!Globals.WaitingOnServer && _createButton.IsDisabled)
        {
            _createButton.Enable();
        }
    }

    public override void Show() => Show(force: false);

    public void Show(bool force)
    {
        _backButton.IsVisibleInTree = !force;
        _createButton.Alignment = force ? [Alignments.Center] : [Alignments.Left];

        if (_renderLayers == null)
        {
            _renderLayers = new ImagePanel[Options.Instance.Equipment.Paperdoll.Down.Count];
            for (var i = 0; i < _renderLayers.Length; i++)
            {
                _renderLayers[i] = new ImagePanel(_preview)
                {
                    Alignment = [Alignments.Center],
                    RestrictToParent = false,
                };
            }
        }

        UpdateDisplay();
        base.Show();
    }

    //Methods
    private void UpdateDisplay()
    {
        if (_renderLayers == null)
        {
            return;
        }

        var classDescriptor = GetClass();
        if (classDescriptor == default || _displaySpriteIndex == -1 || classDescriptor.Sprites.Count <= 0)
        {
            foreach (var renderLayer in _renderLayers)
            {
                renderLayer.Hide();
            }

            return;
        }

        var source = _genderMaleCheckbox.IsChecked
            ? _maleSprites[_displaySpriteIndex]
            : _femaleSprites[_displaySpriteIndex];

        var paperdollOrder = Options.Instance.Equipment.Paperdoll.Directions[_previewDirection];
        var appearance = GetCurrentAppearance();

        for (var layerIndex = 0; layerIndex < _renderLayers.Length; layerIndex++)
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
            if (string.Equals("Player", layerType, StringComparison.Ordinal))
            {
                container.Texture = Globals.ContentManager.GetTexture(TextureType.Entity, source.Value.Sprite);
            }
            else if (appearance.TryGetLayer(layerType, out var style, out var color))
            {
                container.Texture = Globals.ContentManager.GetTexture(TextureType.Paperdoll, style);
                container.RenderColor = color;
            }

            var texture = container.Texture;
            if (texture == default)
            {
                container.Hide();
                continue;
            }

            var textureWidth = texture.Width / Options.Instance.Sprites.NormalFrames;
            var textureHeight = texture.Height / Options.Instance.Sprites.Directions;
            if (textureWidth <= 0 || textureHeight <= 0)
            {
                container.Hide();
                continue;
            }

            container.SetTextureRect(0, _previewDirection * textureHeight, textureWidth, textureHeight);
            _ = container.SetSize(textureWidth, textureHeight);
            container.SetPosition(
                (_preview.Width - textureWidth) / 2,
                (_preview.Height - textureHeight) / 2
            );
            container.Show();
        }
    }

    private ClassDescriptor? GetClass()
    {
        if (_classCombobox.SelectedItem == null)
        {
            return null;
        }

        return ClassDescriptor.Lookup.Values.OfType<ClassDescriptor>().FirstOrDefault(
            descriptor =>
                !descriptor.Locked &&
                string.Equals(_classCombobox.SelectedItem.Text, descriptor.Name, StringComparison.Ordinal)
        );
    }

    private void LoadClass()
    {
        var cls = GetClass();
        _maleSprites.Clear();
        _femaleSprites.Clear();
        _displaySpriteIndex = -1;
        if (cls != null)
        {
            for (var i = 0; i < cls.Sprites.Count; i++)
            {
                if (cls.Sprites[i].Gender == 0)
                {
                    _maleSprites.Add(new KeyValuePair<int, ClassSprite>(i, cls.Sprites[i]));
                }
                else
                {
                    _femaleSprites.Add(new KeyValuePair<int, ClassSprite>(i, cls.Sprites[i]));
                }
            }
        }

        ResetSprite();
    }

    private void ResetSprite()
    {
        _nextSpriteButton.IsHidden = true;
        _prevSpriteButton.IsHidden = true;

        if (_genderMaleCheckbox.IsChecked)
        {
            if (_maleSprites.Count > 0)
            {
                _displaySpriteIndex = 0;
                if (_maleSprites.Count > 1)
                {
                    _nextSpriteButton.IsHidden = false;
                    _prevSpriteButton.IsHidden = false;
                }
            }
            else
            {
                _displaySpriteIndex = -1;
            }
        }
        else
        {
            if (_femaleSprites.Count > 0)
            {
                _displaySpriteIndex = 0;
                if (_femaleSprites.Count > 1)
                {
                    _nextSpriteButton.IsHidden = false;
                    _prevSpriteButton.IsHidden = false;
                }
            }
            else
            {
                _displaySpriteIndex = -1;
            }
        }
    }

    private void _prevSpriteButton_Clicked(Base sender, MouseButtonState arguments)
    {
        _displaySpriteIndex--;
        if (_genderMaleCheckbox.IsChecked)
        {
            if (_maleSprites.Count > 0)
            {
                if (_displaySpriteIndex == -1)
                {
                    _displaySpriteIndex = _maleSprites.Count - 1;
                }
            }
            else
            {
                _displaySpriteIndex = -1;
            }
        }
        else
        {
            if (_femaleSprites.Count > 0)
            {
                if (_displaySpriteIndex == -1)
                {
                    _displaySpriteIndex = _femaleSprites.Count - 1;
                }
            }
            else
            {
                _displaySpriteIndex = -1;
            }
        }

        UpdateDisplay();
    }

    private void _nextSpriteButton_Clicked(Base sender, MouseButtonState arguments)
    {
        _displaySpriteIndex++;
        if (_genderMaleCheckbox.IsChecked)
        {
            if (_maleSprites.Count > 0)
            {
                if (_displaySpriteIndex >= _maleSprites.Count)
                {
                    _displaySpriteIndex = 0;
                }
            }
            else
            {
                _displaySpriteIndex = -1;
            }
        }
        else
        {
            if (_femaleSprites.Count > 0)
            {
                if (_displaySpriteIndex >= _femaleSprites.Count)
                {
                    _displaySpriteIndex = 0;
                }
            }
            else
            {
                _displaySpriteIndex = -1;
            }
        }

        UpdateDisplay();
    }

    private LabeledComboBox CreateAppearanceCombo(string name, string label)
    {
        var combo = new LabeledComboBox(_propertiesPanel, name: name)
        {
            AutoSizeToContents = false,
            Dock = Pos.Top,
            Font = _defaultFont,
            FontSize = 11,
            Label = label,
            MinimumSize = new Point(240, 28),
        };
        combo.ItemSelected += (_, _) => UpdateDisplay();
        return combo;
    }

    private void AddDirectionButton(string text, int direction)
    {
        var button = new Button(_directionPanel)
        {
            Font = _defaultFont,
            FontSize = 10,
            MinimumSize = new Point(72, 24),
            Text = text,
        };
        button.Clicked += (_, _) =>
        {
            _previewDirection = direction;
            UpdateDisplay();
        };
    }

    private static bool IsBaseAppearanceTexture(string name, string prefix)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !name.EndsWith("_attack.png", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("_cast.png", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("_idle.png", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("_shoot.png", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith("_weapon.png", StringComparison.OrdinalIgnoreCase);
    }

    private static string FriendlyAppearanceName(string fileName, string prefix)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            name = name[prefix.Length..];
        }

        return string.Join(
            " ",
            name.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..])
        );
    }

    private void PopulateStyleCombo(LabeledComboBox combo, string prefix)
    {
        combo.ClearItems();
        var none = combo.AddItem("None", userData: string.Empty);
        combo.SelectedItem = none;

        var names = GameContentManager.Current.GetTextureNames(TextureType.Paperdoll) ?? [];
        foreach (var name in names
                     .Where(value => IsBaseAppearanceTexture(value, prefix))
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            combo.AddItem(FriendlyAppearanceName(name, prefix), userData: name);
        }

        if (names.Any(value => IsBaseAppearanceTexture(value, prefix)))
        {
            var first = names
                .Where(value => IsBaseAppearanceTexture(value, prefix))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .First();
            combo.SelectByUserData(first);
        }
    }

    private void PopulateColorCombo(LabeledComboBox combo, bool hair)
    {
        combo.ClearItems();
        var colors = hair
            ? new (string, Color)[]
            {
                ("Black", new Color(38, 30, 30)),
                ("Dark Brown", new Color(74, 48, 35)),
                ("Brown", new Color(116, 74, 46)),
                ("Auburn", new Color(145, 67, 39)),
                ("Blonde", new Color(210, 176, 98)),
                ("Silver", new Color(174, 176, 181)),
                ("White", Color.White),
                ("Blue", new Color(61, 104, 173)),
            }
            : new (string, Color)[]
            {
                ("Navy", new Color(42, 63, 104)),
                ("Burgundy", new Color(118, 48, 58)),
                ("Forest", new Color(55, 99, 63)),
                ("Brown", new Color(112, 75, 48)),
                ("Charcoal", new Color(58, 60, 66)),
                ("Gray", new Color(125, 127, 132)),
                ("Cream", new Color(222, 211, 178)),
                ("Gold", new Color(190, 151, 62)),
            };

        MenuItem? first = null;
        foreach (var (label, color) in colors)
        {
            var item = combo.AddItem(label, userData: color);
            first ??= item;
        }

        combo.SelectedItem = first;
    }

    private void PopulateAppearanceControls()
    {
        PopulateStyleCombo(_hairCombobox, CharacterAppearance.HairPrefix);
        PopulateStyleCombo(_shirtCombobox, CharacterAppearance.ShirtPrefix);
        PopulateStyleCombo(_pantsCombobox, CharacterAppearance.PantsPrefix);
        PopulateStyleCombo(_bootsCombobox, CharacterAppearance.BootsPrefix);

        PopulateColorCombo(_hairColorCombobox, hair: true);
        PopulateColorCombo(_shirtColorCombobox, hair: false);
        PopulateColorCombo(_pantsColorCombobox, hair: false);
        PopulateColorCombo(_bootsColorCombobox, hair: false);
    }

    private static string SelectedStyle(LabeledComboBox combo) =>
        combo.SelectedItem?.UserData as string ?? string.Empty;

    private static Color SelectedColor(LabeledComboBox combo) =>
        combo.SelectedItem?.UserData is Color color ? new Color(color) : Color.White;

    private CharacterAppearance GetCurrentAppearance() =>
        new CharacterAppearance
        {
            HairStyle = SelectedStyle(_hairCombobox),
            HairColor = SelectedColor(_hairColorCombobox),
            ShirtStyle = SelectedStyle(_shirtCombobox),
            ShirtColor = SelectedColor(_shirtColorCombobox),
            PantsStyle = SelectedStyle(_pantsCombobox),
            PantsColor = SelectedColor(_pantsColorCombobox),
            BootsStyle = SelectedStyle(_bootsCombobox),
            BootsColor = SelectedColor(_bootsColorCombobox),
        }.SanitizedCopy();

    private void RandomizeAppearance()
    {
        RandomizeStyle(_hairCombobox, CharacterAppearance.HairPrefix);
        RandomizeStyle(_shirtCombobox, CharacterAppearance.ShirtPrefix);
        RandomizeStyle(_pantsCombobox, CharacterAppearance.PantsPrefix);
        RandomizeStyle(_bootsCombobox, CharacterAppearance.BootsPrefix);

        RandomizeColor(_hairColorCombobox);
        RandomizeColor(_shirtColorCombobox);
        RandomizeColor(_pantsColorCombobox);
        RandomizeColor(_bootsColorCombobox);
        UpdateDisplay();
    }

    private void RandomizeStyle(LabeledComboBox combo, string prefix)
    {
        var values = (GameContentManager.Current.GetTextureNames(TextureType.Paperdoll) ?? [])
            .Where(value => IsBaseAppearanceTexture(value, prefix))
            .ToArray();

        if (values.Length > 0)
        {
            combo.SelectByUserData(values[_random.Next(values.Length)]);
        }
    }

    private void RandomizeColor(LabeledComboBox combo)
    {
        var palette = combo == _hairColorCombobox
            ? new[] { "Black", "Dark Brown", "Brown", "Auburn", "Blonde", "Silver", "White", "Blue" }
            : new[] { "Navy", "Burgundy", "Forest", "Brown", "Charcoal", "Gray", "Cream", "Gold" };
        combo.SelectByText(palette[_random.Next(palette.Length)]);
    }

    void TryCreateCharacter()
    {
        var cls = GetClass();
        if (Globals.WaitingOnServer || _displaySpriteIndex == -1 || cls == default)
        {
            return;
        }

        if (!FieldChecking.IsValidUsername(_nameInput.Text, Strings.Regex.Username))
        {
            Interface.ShowAlert(Strings.CharacterCreation.InvalidName, alertType: AlertType.Error);
            return;
        }

        var charName = _nameInput.Text;
        var spriteKey = _genderMaleCheckbox.IsChecked ? _maleSprites[_displaySpriteIndex].Key : _femaleSprites[_displaySpriteIndex].Key;

        PacketSender.SendCreateCharacter(charName, cls.Id, spriteKey, GetCurrentAppearance());
        Globals.WaitingOnServer = true;
        _createButton.Disable();
        ChatboxMsg.ClearMessages();
    }

    //Input Handlers
    void classCombobox_ItemSelected(Base control, ItemSelectedEventArgs args)
    {
        LoadClass();
        UpdateDisplay();
    }

    void MaleCheckboxGenderChecked(ICheckbox sender, EventArgs arguments)
    {
        _genderMaleCheckbox.IsChecked = true;
        _genderFemaleCheckbox.IsChecked = false;
        ResetSprite();
        UpdateDisplay();
    }

    void FemaleCheckboxGenderChecked(ICheckbox sender, EventArgs arguments)
    {
        _genderFemaleCheckbox.IsChecked = true;
        _genderMaleCheckbox.IsChecked = false;
        ResetSprite();
        UpdateDisplay();
    }

    void CreateButton_Clicked(Base sender, MouseButtonState arguments)
    {
        if (Globals.WaitingOnServer)
        {
            return;
        }

        TryCreateCharacter();
    }

    private void BackButton_Clicked(Base sender, MouseButtonState arguments)
    {
        Hide();
        if (Options.Instance.Player.MaxCharacters <= 1)
        {
            //Logout
            _mainMenu.Show();
        }
        else
        {
            //Character Selection Screen
            _selectCharacterWindow.Show();
        }
    }
}
