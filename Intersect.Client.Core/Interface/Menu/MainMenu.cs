using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Networking;
using Intersect.Client.ThirdParty;
using Intersect.Framework;
using Intersect.Framework.Core;
using Intersect.Network;
using Intersect.Core;
using Microsoft.Extensions.Logging;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Client.Interface.Menu;

public partial class MainMenu : MutableInterface
{
    private readonly Canvas _menuCanvas;
    private readonly MainMenuWindow _mainMenuWindow;

    private bool _shouldOpenCharacterCreation;
    private bool _shouldOpenCharacterSelection;
    private bool _forceCharacterCreation;

    private string? _username;
    private bool _steamAutoLoginAttempted;
    private bool _steamAutoLoginInProgress;

    internal bool SteamAutoLoginInProgress => _steamAutoLoginInProgress;

    // Network status
    public static NetworkStatus ActiveNetworkStatus { get; set; }

    public delegate void NetworkStatusHandler();

    public static event NetworkStatusHandler? NetworkStatusChanged;
    internal static event EventHandler? ReceivedConfiguration;

    public static long LastNetworkStatusChangeTime { get; private set; }

    private LoginWindow? _loginWindow;

    private LoginWindow LoginWindow => _loginWindow ??= new LoginWindow(_menuCanvas, this)
    {
        Alignment = [Alignments.CenterH],
        Y = 480,
        IsVisibleInTree = false,
    };

    private RegistrationWindow? _registrationWindow;

    private RegistrationWindow RegistrationWindow => _registrationWindow ??= new RegistrationWindow(_menuCanvas, this)
    {
        Alignment = [Alignments.CenterH],
        Y = 480,
        IsVisibleInTree = false,
    };

    private ForgotPasswordWindow? _forgotPasswordWindow;

    private ForgotPasswordWindow ForgotPasswordWindow => _forgotPasswordWindow ??= new ForgotPasswordWindow(_menuCanvas)
    {
        // Alignment = [Alignments.CenterH],
        // Y = 480,
        // IsVisible = false,
    };

    private PasswordChangeWindow? _passwordChangeWindow;

    private CharacterCreationWindow? _characterCreationWindow;

    private CharacterCreationWindow CharacterCreationWindow => _characterCreationWindow ??= new CharacterCreationWindow(_menuCanvas, this, SelectCharacterWindow)
    {
        Alignment = [Alignments.CenterH],
        Y = 480,
        IsVisibleInTree = false,
    };

    private CreditsWindow? _creditsWindow;

    private CreditsWindow CreditsWindow => _creditsWindow ??= new CreditsWindow(_menuCanvas, this)
    {
        Alignment = [Alignments.CenterH],
        Y = 480,
        IsVisibleInTree = false,
    };

    private SelectCharacterWindow? _selectCharacterWindow;

    public SelectCharacterWindow SelectCharacterWindow => _selectCharacterWindow ??=
        new SelectCharacterWindow(_menuCanvas, this)
        {
            Alignment = [Alignments.CenterH],
            Y = 480,
            IsVisibleInTree = false,
        };

    private SettingsWindow? _settingsWindow;

    private SettingsWindow SettingsWindow => _settingsWindow ??= new SettingsWindow(_menuCanvas)
    {
        Alignment = [Alignments.CenterH],
        Y = 480,
        IsVisibleInTree = false,
    };

    public MainMenu(Canvas menuCanvas) : base(menuCanvas)
    {
        _menuCanvas = menuCanvas;
        _mainMenuWindow = new MainMenuWindow(_menuCanvas, this)
        {
            Alignment = [Alignments.CenterH],
            Y = 480,
            IsVisibleInTree = true,
        };

        var logo = new ImagePanel(menuCanvas, "Logo");
        logo.LoadJsonUi(GameContentManager.UI.Menu, Graphics.Renderer.GetResolutionString());

        NetworkStatusChanged += HandleNetworkStatusChanged;
        ReceivedConfiguration += HandleSteamAutoLogin;
    }

    ~MainMenu()
    {
        // Finalizer fallback only. Normal UI teardown uses DetachEventHandlers() explicitly.
        // ReSharper disable once DelegateSubtraction
        NetworkStatusChanged -= HandleNetworkStatusChanged;
        ReceivedConfiguration -= HandleSteamAutoLogin;
    }

    internal void DetachEventHandlers()
    {
        // ReSharper disable once DelegateSubtraction
        NetworkStatusChanged -= HandleNetworkStatusChanged;
        ReceivedConfiguration -= HandleSteamAutoLogin;
        _mainMenuWindow.DetachEventHandlers();
    }

    public static void HandleReceivedConfiguration()
    {
        ReceivedConfiguration?.Invoke(default, EventArgs.Empty);
    }

    private void HandleSteamAutoLogin(object? sender, EventArgs eventArgs)
    {
        ApplicationContext.Context.Value?.Logger.LogInformation(
            "Steam auto-login trigger received. Initialized={SteamInitialized}, Connected={Connected}, AlreadyAttempted={AlreadyAttempted}",
            Steam.Initialized,
            ClientNetwork.IsConnected,
            _steamAutoLoginAttempted
        );

        if (_steamAutoLoginAttempted || !Steam.Initialized || !ClientNetwork.IsConnected)
        {
            return;
        }

        _steamAutoLoginAttempted = true;
        _steamAutoLoginInProgress = true;
        Globals.WaitingOnServer = true;

        // A Steam launch is authoritative while the automatic authentication flow is active.
        // Never leave the classic username/password window interactive in parallel.
        _loginWindow?.Hide();
        _mainMenuWindow.Show();

        var ticketRequestStarted = Steam.TryRequestCorpsRoyauxLoginTicket(
            ticket =>
            {
                if (string.IsNullOrWhiteSpace(ticket) || !ClientNetwork.IsConnected)
                {
                    CancelSteamAutoLogin();
                    return;
                }

                ApplicationContext.Context.Value?.Logger.LogInformation(
                    "Sending Steam authentication ticket to the Corps Royaux server."
                );
                PacketSender.SendSteamLogin(ticket);
            }
        );

        if (!ticketRequestStarted)
        {
            CancelSteamAutoLogin();
        }
    }

    internal void CancelSteamAutoLogin()
    {
        if (!_steamAutoLoginInProgress)
        {
            return;
        }

        _steamAutoLoginInProgress = false;
        Globals.WaitingOnServer = false;
        _mainMenuWindow.UpdateDisabled();

        ApplicationContext.Context.Value?.Logger.LogInformation(
            "Steam automatic login ended before authentication completed; classic login is available again."
        );
    }

    //Methods
    public void Update(TimeSpan elapsed, TimeSpan total)
    {
        if (_mainMenuWindow.IsVisibleInTree)
        {
            _mainMenuWindow.Update();
        }

        if (_shouldOpenCharacterSelection)
        {
            CreateCharacterSelection();
        }

        if (_shouldOpenCharacterCreation)
        {
            CreateCharacterCreation();
        }

        if (_loginWindow is { IsVisibleInTree: true } loginWindow)
        {
            loginWindow.Update();
        }

        if (_characterCreationWindow is { IsVisibleInTree: true } characterCreationWindow)
        {
            characterCreationWindow.Update();
        }

        if (_registrationWindow is { IsVisibleInTree: true } registrationWindow)
        {
            registrationWindow.Update();
        }

        if (_selectCharacterWindow is { IsVisibleInTree: true } selectCharacterWindow)
        {
            selectCharacterWindow.Update();
        }

        if (_settingsWindow is { IsVisibleInTree: true } settingsWindow)
        {
            settingsWindow.Update();
        }
    }

    public void Reset()
    {
        _settingsWindow = null;

        _loginWindow?.Hide();
        _registrationWindow?.Hide();
        _creditsWindow?.Hide();
        _forgotPasswordWindow?.Hide();

        _passwordChangeWindow?.DelayedDelete();
        _passwordChangeWindow = null;

        _characterCreationWindow?.Hide();
        _selectCharacterWindow?.Hide();
        _mainMenuWindow.Show();
        _mainMenuWindow.Reset();
    }

    public void Show() => _mainMenuWindow.Show();

    private void Hide() => _mainMenuWindow.Hide();

    public void NotifyOpenCharacterSelection(
        List<CharacterSelectionPreviewMetadata> characterSelectionPreviews,
        string username
    )
    {
        _username = username;
        _shouldOpenCharacterSelection = true;
        SelectCharacterWindow.CharacterSelectionPreviews = [..characterSelectionPreviews];
    }

    public void NotifyOpenForgotPassword()
    {
        Reset();
        Hide();
        ForgotPasswordWindow.Show();
    }

    public void NotifyOpenLogin()
    {
        Reset();
        Hide();
        LoginWindow.Show();
    }

    public void OpenPasswordChangeWindow(string? identifier, PasswordChangeMode changeMode, Window? previousWindow)
    {
        Reset();
        Hide();

        _passwordChangeWindow?.Dispose();

        identifier ??= _username;

        previousWindow ??= changeMode switch
        {
            PasswordChangeMode.ResetToken => LoginWindow,
            PasswordChangeMode.ExistingPassword => SelectCharacterWindow,
            _ => throw Exceptions.UnreachableInvalidEnum(changeMode),
        };

        _passwordChangeWindow = new PasswordChangeWindow(
            _menuCanvas,
            this,
            previousWindow,
            changeMode
        )
        {
            // Alignment = [Alignments.CenterH],
            // Y = 480,
            // IsVisible = false,
            Target = identifier,
        };
        _passwordChangeWindow.Disposed += PasswordChangeWindowOnDisposed;
    }

    private void PasswordChangeWindowOnDisposed(Base sender, EventArgs _)
    {
        if (sender == _passwordChangeWindow)
        {
            _passwordChangeWindow = null;
        }
    }

    private void CreateCharacterSelection()
    {
        _steamAutoLoginInProgress = false;
        Hide();
        LoginWindow.Hide();
        RegistrationWindow.Hide();
        SettingsWindow.Hide();
        CharacterCreationWindow.Hide();
        SelectCharacterWindow.Show();
        _shouldOpenCharacterSelection = false;
    }

    public void NotifyOpenCharacterCreation(bool force = false)
    {
        _forceCharacterCreation = force;
        _shouldOpenCharacterCreation = true;
    }

    private void CreateCharacterCreation()
    {
        _steamAutoLoginInProgress = false;
        Hide();
        LoginWindow.Hide();
        RegistrationWindow.Hide();
        SettingsWindow.Hide();
        SelectCharacterWindow.Hide();
        CharacterCreationWindow.Show(force: _forceCharacterCreation);
        _shouldOpenCharacterCreation = false;
    }

    internal void SwitchToWindow<TMainMenuWindow>() where TMainMenuWindow : IMainMenuWindow
    {
        if (_steamAutoLoginInProgress &&
            (typeof(TMainMenuWindow) == typeof(LoginWindow) ||
             typeof(TMainMenuWindow) == typeof(RegistrationWindow)))
        {
            _mainMenuWindow.Show();
            return;
        }

        _mainMenuWindow.Hide();
        if (typeof(TMainMenuWindow) == typeof(LoginWindow))
        {
            LoginWindow.Show();
        }
        else if (typeof(TMainMenuWindow) == typeof(RegistrationWindow))
        {
            RegistrationWindow.Show();
        }
        else if (typeof(TMainMenuWindow) == typeof(CreditsWindow))
        {
            CreditsWindow.Show();
        }
    }

    internal void SettingsButton_Clicked()
    {
        Hide();
        SettingsWindow.Show(_mainMenuWindow);
    }

    private void HandleNetworkStatusChanged() => _mainMenuWindow.UpdateDisabled();

    public static void SetNetworkStatus(NetworkStatus networkStatus, bool resetStatusCheck = false)
    {
        if (ActiveNetworkStatus != networkStatus)
        {
            ActiveNetworkStatus = networkStatus;
            NetworkStatusChanged?.Invoke();
        }
        LastNetworkStatusChangeTime = resetStatusCheck ? -1 : Timing.Global.MillisecondsUtc;
    }
}
