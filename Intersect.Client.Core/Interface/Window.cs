using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.General;
using Base = Intersect.Client.Framework.Gwen.Control.Base;

namespace Intersect.Client.Interface;

public abstract class Window : WindowControl
{
    private delegate void Initializer(Intersect.Client.Framework.Gwen.Skin.Base skin);

    private readonly object _initializationLock = new();
    private volatile bool _initialized;

    private Initializer? _initializer;

    protected Window(
        Base parent,
        string? title = default,
        bool modal = false,
        string? name = default
    ) : base(parent, title, modal, name)
    {
        _initializer = Initialize;

        SetTextColor(Color.White, ControlState.Active);
        SetTextColor(new Color(a: 255, r: 191, g: 191, b: 191), ControlState.Inactive);

        SkipRender();
    }

    protected override void OnPreDraw(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        base.OnPreDraw(skin);

        if (_initializer is { } initializer)
        {
            _initializer = null;
            initializer(skin);
        }
#if ANDROID
        if (General.Globals.GameState == GameStates.InGame && Interface.MobilePanelIsEligible(this) &&
            Parent is Framework.Gwen.Control.Canvas or Modal)
        {
            PresentFullscreen(Interface.MobileWindowBounds, Localization.ClientLanguages.Text("Close"),
                this is Game.Inventory.InventoryWindow ? Interface.GameUi.CloseMobileInventorySession : null);
            TitleLabel.Font = Framework.File_Management.GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont;
            DoLayoutIfNeeded(skin);
        }
#endif
    }

    private void Initialize(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        lock (_initializationLock)
        {
            EnsureInitialized();
            DoLayoutIfNeeded(skin);
        }
    }

    protected abstract void EnsureInitialized();
}
