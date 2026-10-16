using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Utilities;

namespace Intersect.Client.Interface.Game.Bank;

public partial class BankWindow : Window
{
    public static int BaseSlotCount { get; set; }
    public static bool Premium { get; set; }
    private int _tab;
    private Base? _bankHeader;
    private Label? _premiumHint;
    public List<SlotItem> Items = [];
    private readonly ScrollControl _slotContainer;
    private readonly ContextMenu _contextMenu;

    //Init
    public BankWindow(Canvas gameCanvas) : base(
        gameCanvas,
        Globals.IsGuildBank
            ? Strings.Guilds.Bank.ToString(Globals.Me?.Guild)
            : Strings.Bank.Title.ToString(),
        false,
        nameof(BankWindow)
    )
    {
        DisableResizing();
        Interface.InputBlockingComponents.Add(this);

        Alignment = [Alignments.Center];
        MinimumSize = new Point(x: 436, y: 454);
        IsResizable = false;
        IsClosable = true;

        TitleLabel.FontSize = 14;
        TitleLabel.TextColorOverride = Color.White;

        Closed += (b, s) =>
        {
            _contextMenu?.Close();
            Interface.GameUi.NotifyCloseBank();
        };

        _slotContainer = new ScrollControl(this, "ItemContainer")
        {
            Dock = Pos.Fill,
            OverflowX = OverflowBehavior.Auto,
            OverflowY = OverflowBehavior.Scroll,
        };

        _contextMenu = new ContextMenu(gameCanvas, "BankContextMenu")
        {
            IsVisibleInParent = false,
            IconMarginDisabled = true,
            ItemFont = GameContentManager.Current.GetFont(name: "sourcesansproblack"),
            ItemFontSize = 10,
        };
    }

    protected override void EnsureInitialized()
    {
        LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());
        var header = _bankHeader = new Base(this, "BankAccountTabs") { Dock = Pos.Top, Height = 64 };
        var regular = new Button(header) { Text = ClientLanguages.Text("Storage"), FontSize = 10 };
        regular.SetBounds(6, 3, 115, 28); regular.Clicked += (_, _) => SelectTab(0);
        var premium = new Button(header) { Text = ClientLanguages.Text("Premium storage"), FontSize = 10, IsDisabled = Globals.IsGuildBank || Globals.BankSlotCount <= BaseSlotCount };
        premium.SetBounds(125, 3, 150, 28); premium.Clicked += (_, _) => SelectTab(1);
        var sort = new Button(header) { Text = ClientLanguages.Text("Auto sort"), FontSize = 10, IsDisabled = !Premium || Globals.IsGuildBank };
        sort.SetBounds(279, 3, 120, 28); sort.Clicked += (_, _) => Intersect.Client.Networking.Network.SendPacket(new Intersect.Network.Packets.Client.SortBankPacket());
        _premiumHint = new Label(header) { Text = ClientLanguages.Text(Premium ? "Premium benefits active" : "Premium storage: withdrawals only after expiry"), FontSize = 9, MouseInputEnabled = false };
        _premiumHint.SetBounds(8, 34, 395, 24);
        foreach (var label in header.Children.OfType<Label>()) label.Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont;
        header.IsHidden = Globals.IsGuildBank;
        if (Premium && !Globals.IsGuildBank) TitleLabel.TextColorOverride = new Color(240, 205, 115);
        InitItemContainer();
    }

    private void InitItemContainer()
    {
        for (var slotIndex = 0; slotIndex < Globals.BankSlotCount; slotIndex++)
        {
            Items.Add(new BankItem(this, _slotContainer, slotIndex, _contextMenu));
        }

        SelectTab(0);
    }

    private void SelectTab(int tab)
    {
        _tab=tab;
        var baseSlots=BaseSlotCount>0 ? BaseSlotCount : Globals.BankSlotCount;
        foreach(var item in Items) item.IsHidden = !Globals.IsGuildBank && (tab==0 ? item.SlotIndex>=baseSlots : item.SlotIndex<baseSlots);
        var visible=Items.Where(i=>!i.IsHidden).ToList();
        PopulateSlotContainer.Populate(_slotContainer,visible);
        _slotContainer.ScrollToTop(); _slotContainer.UpdateScrollBars();
    }

    public void Update()
    {
        if (IsVisibleInTree == false)
        {
            return;
        }

        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] is BankItem bankItem)
            {
                bankItem.Update();
            }
        }
    }

    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        base.Render(skin);
        if(!Premium || Globals.IsGuildBank) return;
        var r=skin.Renderer;var b=RenderBounds;r.DrawColor=new Color(255,215,175,80);
        r.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(b.X,b.Y,b.Width,2));
        r.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(b.X,b.Bottom-2,b.Width,2));
        r.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(b.X,b.Y,2,b.Height));
        r.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(b.Right-2,b.Y,2,b.Height));
    }
    public override void Hide()
    {
        _contextMenu?.Close();
        base.Hide();
    }
}
