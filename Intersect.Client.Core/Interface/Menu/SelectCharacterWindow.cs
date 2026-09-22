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

namespace Intersect.Client.Interface.Menu;

public partial class SelectCharacterWindow : Window
{
    private readonly MainMenu _mainMenu;

    private readonly Label _nameLabel;
    private readonly Label _infoLabel;
    private readonly Label _levelCaptionLabel;
    private readonly Label _levelValueLabel;
    private readonly Label _classCaptionLabel;
    private readonly Label _classValueLabel;
    private readonly Label _guildCaptionLabel;
    private readonly Label _guildValueLabel;
    private readonly ImagePanel _preview;
    private readonly Button _selectCharacterRightButton;
    private readonly Button _selectCharacterLeftButton;
    private readonly Button _buttonPlay;
    private readonly Button _buttonDelete;
    private readonly Button _buttonNew;
    private readonly Button _buttonChangePassword;
    private readonly Button _buttonLogout;

    private ImagePanel[]? _renderLayers;

    public CharacterSelectionPreviewMetadata[]? CharacterSelectionPreviews
    {
        get => _characterSelectionPreviews;
        set => SetAndDoIfChanged(ref _characterSelectionPreviews, value, Invalidate);
    }

    public int _selectedCharacterIndex;
    private CharacterSelectionPreviewMetadata[]? _characterSelectionPreviews;

    private readonly IFont? _defaultFont;

    private readonly Panel _characterPreviewPanel;
    private readonly Panel _previewContainer;
    private readonly Panel _buttonsPanel;

    public SelectCharacterWindow(Canvas parent, MainMenu mainMenu) : base(
        parent: parent,
        title: Strings.CharacterSelection.Title,
        modal: false,
        name: nameof(SelectCharacterWindow)
    )
    {
        _mainMenu = mainMenu;

        _defaultFont = GameContentManager.Current.GetFont(name: "sourcesansproblack");

        Alignment = [Alignments.Center];
        MinimumSize = new Point(x: 560, y: 240);
        IsClosable = false;
        IsResizable = false;
        InnerPanelPadding = new Padding(8);
        Titlebar.MouseInputEnabled = false;
        TitleLabel.FontSize = 14;
        TitleLabel.TextColorOverride = Color.White;

        _buttonsPanel = new Panel(this, name: nameof(_buttonsPanel))
        {
            DisplayMode = DisplayMode.FlowStartToEnd,
            Dock = Pos.Bottom,
            DockChildSpacing = new Padding(8),
            Margin = new Margin(0, 8, 0, 0),
            ShouldDrawBackground = false,
        };

        _buttonNew = new Button(_buttonsPanel, name: nameof(_buttonNew))
        {
            Alignment = [Alignments.Left],
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(140, 24),
            Text = Strings.CharacterSelection.New,
        };
        _buttonNew.Clicked += _buttonNew_Clicked;

        _buttonPlay = new Button(_buttonsPanel, name: nameof(_buttonPlay))
        {
            Alignment = [Alignments.Left],
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(160, 24),
            Text = Strings.CharacterSelection.Play,
        };
        _buttonPlay.Clicked += ButtonPlay_Clicked;

        _buttonDelete = new Button(_buttonsPanel, name: nameof(_buttonDelete))
        {
            Alignment = [Alignments.CenterH],
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(160, 24),
            Text = Strings.CharacterSelection.Delete,
        };
        _buttonDelete.Clicked += _buttonDelete_Clicked;

        _buttonChangePassword = new Button(_buttonsPanel, name: nameof(_buttonLogout))
        {
            Alignment = [Alignments.Right],
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(160, 24),
            Text = Strings.CharacterSelection.ChangePassword,
        };
        _buttonChangePassword.Clicked += ButtonChangePasswordOnClicked;

        _buttonLogout = new Button(_buttonsPanel, name: nameof(_buttonLogout))
        {
            Alignment = [Alignments.Right],
            Font = _defaultFont,
            FontSize = 12,
            MinimumSize = new Point(160, 24),
            Text = Strings.CharacterSelection.Logout,
        };
        _buttonLogout.Clicked += _buttonLogout_Clicked;

        _characterPreviewPanel = new Panel(this, name: nameof(_characterPreviewPanel))
        {
            Dock = Pos.Fill,
            ShouldDrawBackground = false,
        };

        _selectCharacterLeftButton = new Button(_characterPreviewPanel, name: nameof(_selectCharacterLeftButton), disableText: true)
        {
            Dock = Pos.Left | Pos.CenterV,
            MinimumSize = new Point(30, 35),
            MaximumSize = new Point(30, 35),
        };
        _selectCharacterLeftButton.Clicked += SelectCharacterLeftButtonOnClicked;
        _selectCharacterLeftButton.SetStateTexture(ComponentState.Normal, "button.arrow_left.normal.png");
        _selectCharacterLeftButton.SetStateTexture(ComponentState.Disabled, "button.arrow_left.disabled.png");
        _selectCharacterLeftButton.SetStateTexture(ComponentState.Hovered, "button.arrow_left.hovered.png");
        _selectCharacterLeftButton.SetStateTexture(ComponentState.Active, "button.arrow_left.active.png");

        _selectCharacterRightButton = new Button(_characterPreviewPanel, name: nameof(_selectCharacterRightButton), disableText: true)
        {
            Dock = Pos.Right | Pos.CenterV,
            MinimumSize = new Point(30, 35),
            MaximumSize = new Point(30, 35),
        };
        _selectCharacterRightButton.Clicked += SelectCharacterRightButtonOnClicked;
        _selectCharacterRightButton.SetStateTexture(ComponentState.Normal, "button.arrow_right.normal.png");
        _selectCharacterRightButton.SetStateTexture(ComponentState.Disabled, "button.arrow_right.disabled.png");
        _selectCharacterRightButton.SetStateTexture(ComponentState.Hovered, "button.arrow_right.hovered.png");
        _selectCharacterRightButton.SetStateTexture(ComponentState.Active, "button.arrow_right.active.png");

        _infoLabel = new Label(_characterPreviewPanel, name: nameof(_infoLabel))
        {
            AutoSizeToContents = true,
            Dock = Pos.Bottom | Pos.CenterH,
            Font = _defaultFont,
            FontSize = 12,
            Text = Strings.CharacterSelection.Empty,
        };

        _nameLabel = new Label(_characterPreviewPanel, name: nameof(_nameLabel))
        {
            AutoSizeToContents = true,
            Dock = Pos.Bottom | Pos.CenterH,
            Font = _defaultFont,
            FontSize = 12,
        };

        _levelCaptionLabel = new Label(_characterPreviewPanel, name: nameof(_levelCaptionLabel))
        {
            Font = _defaultFont,
            FontSize = 12,
        };
        _levelValueLabel = new Label(_characterPreviewPanel, name: nameof(_levelValueLabel))
        {
            Font = _defaultFont,
            FontSize = 12,
        };
        _classCaptionLabel = new Label(_characterPreviewPanel, name: nameof(_classCaptionLabel))
        {
            Font = _defaultFont,
            FontSize = 12,
        };
        _classValueLabel = new Label(_characterPreviewPanel, name: nameof(_classValueLabel))
        {
            Font = _defaultFont,
            FontSize = 12,
        };
        _guildCaptionLabel = new Label(_characterPreviewPanel, name: nameof(_guildCaptionLabel))
        {
            Font = _defaultFont,
            FontSize = 12,
        };
        _guildValueLabel = new Label(_characterPreviewPanel, name: nameof(_guildValueLabel))
        {
            Font = _defaultFont,
            FontSize = 12,
        };

        _previewContainer = new Panel(_characterPreviewPanel, name: nameof(_previewContainer))
        {
            Dock = Pos.Fill,
            ShouldDrawBackground = false,
        };

        _preview = new ImagePanel(_previewContainer, name: nameof(_preview))
        {
            Alignment = [Alignments.Center],
            MaintainAspectRatio = true,
            TextureFilename = "character_preview_background.png",
        };

        _buttonsPanel.SizeToChildren(recursive: true);
    }

    private void ButtonChangePasswordOnClicked(Base sender, MouseButtonState arguments)
    {
        _mainMenu.OpenPasswordChangeWindow(null, PasswordChangeMode.ExistingPassword, this);
    }

    protected override void EnsureInitialized()
    {
        SizeToChildren(recursive: true);

        LoadJsonUi(GameContentManager.UI.Menu, Graphics.Renderer?.GetResolutionString());

        // Character sheet-style profile card. Gwen Label does not reliably render
        // embedded newlines here, so every row is its own label/value pair.
        SetSize(Math.Max(680, Width), Math.Max(300, Height));

        _previewContainer.Dock = Pos.None;
        _previewContainer.SetBounds(78, 48, 112, 112);
        _preview.SetBounds(0, 0, 112, 112);

        static void PlaceProfileLabel(Label label, int x, int y, int width, Color color)
        {
            label.Dock = Pos.None;
            label.AutoSizeToContents = false;
            label.TextAlign = Pos.Left | Pos.CenterV;
            label.TextColorOverride = color;
            label.SetBounds(x, y, width, 22);
        }

        // Color uses ARGB ordering here. Keep CR accents warm gold/brown - never purple.
        var captionColor = new Color(255, 236, 210, 153);
        const int captionX = 205;
        const int valueX = 278;
        const int captionWidth = 68;
        const int valueWidth = 285;
        const int firstRowY = 55;
        const int rowGap = 24;

        PlaceProfileLabel(_infoLabel, captionX, firstRowY, captionWidth, captionColor);
        PlaceProfileLabel(_nameLabel, valueX, firstRowY, valueWidth, Color.White);
        PlaceProfileLabel(_levelCaptionLabel, captionX, firstRowY + rowGap, captionWidth, captionColor);
        PlaceProfileLabel(_levelValueLabel, valueX, firstRowY + rowGap, valueWidth, Color.White);
        PlaceProfileLabel(_classCaptionLabel, captionX, firstRowY + rowGap * 2, captionWidth, captionColor);
        PlaceProfileLabel(_classValueLabel, valueX, firstRowY + rowGap * 2, valueWidth, Color.White);
        PlaceProfileLabel(_guildCaptionLabel, captionX, firstRowY + rowGap * 3, captionWidth, captionColor);
        PlaceProfileLabel(_guildValueLabel, valueX, firstRowY + rowGap * 3, valueWidth, Color.White);

        _infoLabel.Text = "Name:";
        _levelCaptionLabel.Text = "Level:";
        _classCaptionLabel.Text = "Class:";
        _guildCaptionLabel.Text = "Guild:";

        EnsureArrowsVisibility();
    }

    //Methods
    public void Update()
    {
        if (!Networking.Network.IsConnected)
        {
            Hide();
            _mainMenu.Show();
        }

        // Re-Enable our buttons if we're not waiting for the server anymore with it disabled.
        _buttonPlay.IsDisabled = Globals.WaitingOnServer;
        _buttonNew.IsDisabled = Globals.WaitingOnServer;
        _buttonDelete.IsDisabled = Globals.WaitingOnServer;
        _buttonLogout.IsDisabled = Globals.WaitingOnServer;
        _buttonChangePassword.IsDisabled = Globals.WaitingOnServer;
    }

    private void UpdateDisplay()
    {
        if (_renderLayers == default || CharacterSelectionPreviews == default)
        {
            return;
        }

        _selectCharacterRightButton.IsHidden = CharacterSelectionPreviews.Length <= 1;
        _selectCharacterLeftButton.IsHidden = CharacterSelectionPreviews.Length <= 1;

        if (CharacterSelectionPreviews.Length > 1)
        {
            _selectCharacterRightButton.BringToFront();
            _selectCharacterLeftButton.BringToFront();
        }

        foreach (var paperdollPortrait in _renderLayers)
        {
            paperdollPortrait.Texture = default;
            paperdollPortrait.Hide();
        }

        var selectedPreviewMetadata = CharacterSelectionPreviews[_selectedCharacterIndex];
        if (selectedPreviewMetadata == default)
        {
            _buttonPlay.Hide();
            _buttonDelete.Hide();
            _buttonNew.Show();

            _infoLabel.Text = Strings.CharacterSelection.Empty;
            _nameLabel.Text = string.Empty;
            _levelCaptionLabel.Text = string.Empty;
            _levelValueLabel.Text = string.Empty;
            _classCaptionLabel.Text = string.Empty;
            _classValueLabel.Text = string.Empty;
            _guildCaptionLabel.Text = string.Empty;
            _guildValueLabel.Text = string.Empty;
            return;
        }

        _infoLabel.Text = "Name:";
        _nameLabel.Text = selectedPreviewMetadata.Name;
        _levelCaptionLabel.Text = "Level:";
        _levelValueLabel.Text = selectedPreviewMetadata.Level.ToString();
        _classCaptionLabel.Text = "Class:";
        _classValueLabel.Text = selectedPreviewMetadata.Class;
        _guildCaptionLabel.Text = "Guild:";
        _guildValueLabel.Text = string.IsNullOrWhiteSpace(selectedPreviewMetadata.Guild) ? "-" : selectedPreviewMetadata.Guild;

        _buttonPlay.Show();
        _buttonDelete.Show();
        _buttonNew.Hide();

        // Always render the actual character sprite + paperdolls here. A face image
        // hid equipment/appearance changes and made different characters look stale.
        // we are rendering the player facing down, then we need to know the render order of the equipments
        for (var paperdollLayerIndex = 0; paperdollLayerIndex < Options.Instance.Equipment.Paperdoll.Down.Count; paperdollLayerIndex++)
        {
            var paperdollLayerType = Options.Instance.Equipment.Paperdoll.Down[paperdollLayerIndex];
            var paperdollContainer = _renderLayers[paperdollLayerIndex];

            // handle player/equip rendering, we just need to find the correct texture
            if (string.Equals("Player", paperdollLayerType, StringComparison.Ordinal))
            {
                var spriteSource = selectedPreviewMetadata.Sprite;
                var spriteTex = Globals.ContentManager.GetTexture(TextureType.Entity, spriteSource);
                paperdollContainer.Texture = spriteTex;
            }
            else
            {
                if (paperdollLayerIndex >= selectedPreviewMetadata.Equipment.Length)
                {
                    continue;
                }

                var equipFragment = selectedPreviewMetadata.Equipment[paperdollLayerIndex];

                if (equipFragment == default)
                {
                    paperdollContainer.Texture = default;
                    continue;
                }

                paperdollContainer.Texture = Globals.ContentManager.GetTexture(TextureType.Paperdoll, equipFragment.Name);

                if (paperdollContainer.Texture != default)
                {
                    paperdollContainer.RenderColor = equipFragment.RenderColor;
                }
            }

            var layerTex = paperdollContainer.Texture;
            if (layerTex == default)
            {
                paperdollContainer.Hide();
                continue;
            }

            var imgWidth = layerTex.Width;
            var imgHeight = layerTex.Height;
            var textureWidth = imgWidth / Options.Instance.Sprites.NormalFrames;
            var textureHeight = imgHeight / Options.Instance.Sprites.Directions;

            paperdollContainer.SetTextureRect(0, 0, textureWidth, textureHeight);
            _ = paperdollContainer.SetSize(textureWidth, textureHeight);

            var centerX = (_preview.Width / 2) - (paperdollContainer.Width / 2);
            // The portrait background's visible medallion sits above the geometric
            // center of the image panel. Lift every sprite/paperdoll layer together
            // so the character is actually centered inside the circle.
            const int portraitCenterYOffset = -24;
            var centerY = (_preview.Height / 2) - (paperdollContainer.Height / 2) + portraitCenterYOffset;
            paperdollContainer.SetPosition(centerX, centerY);

            paperdollContainer.Show();
        }
    }

    public override void Show()
    {
        if (_renderLayers == default)
        {
            _renderLayers = new ImagePanel[Options.Instance.Equipment.Paperdoll.Down.Count];
            for (var i = 0; i < _renderLayers.Length; i++)
            {
                _renderLayers[i] = new ImagePanel(_preview)
                {
                    Alignment = [Alignments.Center],
                };
            }
        }

        if (CharacterSelectionPreviews == default)
        {
            CharacterSelectionPreviews = new CharacterSelectionPreviewMetadata[Options.Instance.Player.MaxCharacters];
        }

        _selectedCharacterIndex = 0;
        UpdateDisplay();

        if (_buttonPlay.IsVisibleInParent)
        {
            PostLayout.Enqueue(button => button.Focus(), _buttonPlay);
        }

        base.Show();
        EnsureArrowsVisibility();
    }

    private void EnsureArrowsVisibility()
    {
        // in case the json load is changing the visibility of the arrows when it shouldn't
        // lets ensure the right value
        if (CharacterSelectionPreviews != default)
        {
            _selectCharacterRightButton.IsHidden = CharacterSelectionPreviews.Length <= 1;
            _selectCharacterLeftButton.IsHidden = CharacterSelectionPreviews.Length <= 1;
        }
    }

    private void _buttonLogout_Clicked(Base sender, MouseButtonState arguments)
    {
        Main.Logout(false, skipFade: true);
        // _mainMenu.Reset();
    }

    private void SelectCharacterLeftButtonOnClicked(Base sender, MouseButtonState arguments)
    {
        _selectedCharacterIndex--;
        if (_selectedCharacterIndex < 0)
        {
            _selectedCharacterIndex = CharacterSelectionPreviews!.Length - 1;
        }

        UpdateDisplay();
    }

    private void SelectCharacterRightButtonOnClicked(Base sender, MouseButtonState arguments)
    {
        _selectedCharacterIndex++;
        if (_selectedCharacterIndex >= CharacterSelectionPreviews!.Length)
        {
            _selectedCharacterIndex = 0;
        }

        UpdateDisplay();
    }

    private void _buttonDelete_Clicked(Base sender, MouseButtonState arguments)
    {
        if (Globals.WaitingOnServer || CharacterSelectionPreviews == default)
        {
            return;
        }

        _ = new InputBox(
            title: Strings.CharacterSelection.DeleteTitle.ToString(CharacterSelectionPreviews[_selectedCharacterIndex].Name),
            prompt: Strings.CharacterSelection.DeletePrompt.ToString(CharacterSelectionPreviews[_selectedCharacterIndex].Name),
            inputType: InputType.YesNo,
            userData: CharacterSelectionPreviews[_selectedCharacterIndex].Id,
            onSubmit: DeleteCharacter
        );
    }

    private void DeleteCharacter(object? sender, EventArgs e)
    {
        if (sender is InputBox inputBox && inputBox.UserData is Guid charId)
        {
            PacketSender.SendDeleteCharacter(charId);

            Globals.WaitingOnServer = true;
            _buttonPlay.Disable();
            _buttonNew.Disable();
            _buttonDelete.Disable();
            _buttonLogout.Disable();

            _selectedCharacterIndex = 0;
            UpdateDisplay();
        }
    }

    private void _buttonNew_Clicked(Base sender, MouseButtonState arguments)
    {
        if (Globals.WaitingOnServer)
        {
            return;
        }

        PacketSender.SendNewCharacter();

        Globals.WaitingOnServer = true;
        _buttonPlay.Disable();
        _buttonNew.Disable();
        _buttonDelete.Disable();
        _buttonLogout.Disable();
    }

    public void ButtonPlay_Clicked(Base? sender, MouseButtonState? arguments)
    {
        if (Globals.WaitingOnServer || CharacterSelectionPreviews == default)
        {
            return;
        }

        ChatboxMsg.ClearMessages();
        PacketSender.SendSelectCharacter(CharacterSelectionPreviews[_selectedCharacterIndex].Id);

        Globals.WaitingOnServer = true;
        _buttonPlay.Disable();
        _buttonNew.Disable();
        _buttonDelete.Disable();
        _buttonLogout.Disable();
    }
}
