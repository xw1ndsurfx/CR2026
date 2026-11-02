using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.Housing;
namespace Intersect.Client.Interface.Game;
internal sealed class HouseDirectoryWindow : Window
{
    private readonly ScrollControl _rows;
    private readonly TextBox _search;
    private readonly Button _previous, _next;
    private int _page;
    private static string T(string s) => Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(ClientLanguages.Text(s),ClientLanguages.Current);
    internal HouseDirectoryWindow(Canvas parent) : base(parent,T("Accessible houses"),false,nameof(HouseDirectoryWindow))
    {
        SetSize(630, 555); Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _search = new TextBox(this); _search.SetBounds(16, 16, 420, 30);
        var search = new Button(this) { Text = T("Search") }; search.SetBounds(448,16,150,30); search.Clicked += (_,_) => Request(0);
        _rows = new ScrollControl(this) { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll }; _rows.SetBounds(16,62,582,390);
        _previous = new Button(this) { Text = T("Previous") }; _previous.SetBounds(16,465,170,30); _previous.Clicked += (_,_) => Request(_page-1);
        _next = new Button(this) { Text = T("Next") }; _next.SetBounds(206,465,170,30); _next.Clicked += (_,_) => Request(_page+1);
        Hide();
    }
    protected override void EnsureInitialized() { }
    private void Request(int page) => Networking.Network.SendPacket(new HouseActionPacket { Action = HouseAction.Directory, X = Math.Max(0,page), Name = _search.Text });
    internal void Apply(HouseDirectoryPacket packet)
    {
        _page = packet.Page; _rows.DeleteAllChildren(); var y = 0;
        foreach (var house in packet.Houses)
        {
            var label = new Label(_rows) { Text = ClientLanguages.Format("{0}'s house",house.OwnerName), AutoSizeToContents = false, FontSize = 12 }; label.SetBounds(8,y+7,340,30);
            var visit = new Button(_rows) { Text = T("Visit house") }; visit.SetBounds(360,y,190,34);
            visit.Clicked += (_,_) => { Networking.PacketSender.SendHouseAction(HouseAction.Visit,house.OwnerId); Hide(); }; y += 44;
        }
        _rows.SetInnerSize(560,y+8); _previous.IsDisabled = _page<=0; _next.IsDisabled = !packet.HasMore; Show(); BringToFront();
    }
}
public partial class GameInterface
{
    private HouseDirectoryWindow? _houseDirectory;
    public void ApplyHouseDirectory(HouseDirectoryPacket packet) { _houseDirectory ??= new HouseDirectoryWindow(GameCanvas); _houseDirectory.Apply(packet); }
}
