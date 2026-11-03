using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.Client;

namespace Intersect.Client.Interface.Game;

internal static class ContractRewardArt
{
    internal static IGameTexture? Texture(Guid id) => ItemDescriptor.Get(id) is {} item
        ? Intersect.Client.Interface.Game.ItemIconTexture.Get(item) : null;
    internal static string Name(RoyalContractView view) => ItemDescriptor.Get(view.Contract.Template.RewardItemId)?.Name
        ?? view.RewardName.TrimStart('?', ' ');
}

internal static class RoyalContractJournal
{
    internal static RoyalContractBoardState State { get; private set; } = new();
    internal static int Version { get; private set; }
    internal static readonly Queue<RoyalContractView> Completed = new();
    private static readonly HashSet<Guid> Notified = [];
    private static bool _initialized;
    internal static void Reset()
    {
        State=new();Version++;Completed.Clear();Notified.Clear();_initialized=false;
    }
    internal static void Apply(RoyalContractBoardState state)
    {
        if (!state.Enabled && state.Contracts.Count == 0 && state.Message.Length > 0) return;
        foreach (var view in state.Contracts.Where(v => v.Accepted && !v.Claimed && v.Contribution > 0 && v.Progress >= v.Contract.Quantity))
            if (Notified.Add(view.Contract.Id) && _initialized) Completed.Enqueue(view);
        State = state; Version++; _initialized = true;
    }
}

public partial class QuestsWindow
{
    private Guid _selectedContract;
    private int _contractVersion = -1;
    private ImagePanel? _contractReward;
    private static string ContractText(string source) => RoyalContractsText.Get(source, ClientLanguages.Current);
    private RoyalContractView? SelectedContract => RoyalContractJournal.State.Contracts.FirstOrDefault(v => v.Contract.Id == _selectedContract && v.Accepted && !v.Claimed);

    private void AddContractsToJournal()
    {
        var contracts = RoyalContractJournal.State.Contracts.Where(v => v.Accepted && !v.Claimed).ToArray();
        if (contracts.Length == 0) return;
        AddCategoryToList(ContractText("Royal Contracts"), JournalGold);
        foreach (var view in contracts)
        {
            var id = view.Contract.Id;
            var row = _questList.AddRow(ClientLanguages.ContentText(view.Contract.Template.Name) + $" [{view.Progress}/{view.Contract.Quantity}]");
            row.SetTextColor(JournalGold);
            row.Clicked += (_, _) => { _selectedContract = id; mSelectedQuest = null; UpdateSelectedQuest(); };
        }
    }

    private bool ShowContractDetails()
    {
        var view = SelectedContract;
        if (_contractReward != null) _contractReward.Hide();
        mQuestDescLabel.Dock = Pos.Top;
        if (view == null) return false;
        mQuestJournalHeading.Hide(); mQuestJournalSubtitle.Hide(); mQuestJournalSummary.Hide();
        mQuestDetailsProgress.Show(); mQuestTitle.Show(); mQuestStatus.Show(); mQuestDescArea.Show(); mBackButton.Show(); mQuitButton.Hide();
        mQuestTitle.Text = ClientLanguages.ContentText(view.Contract.Template.Name);
        mQuestStatus.Text = ContractText(view.Progress >= view.Contract.Quantity ? "Claim reward" : "In progress");
        mQuestDetailsProgress.Text = string.Format(ContractText("Progress: {0} / {1}"), view.Progress, view.Contract.Quantity);
        var t = view.Contract.Template;
        var body = ClientLanguages.ContentText(t.Description) + "\n\n" + ContractText(RoyalContractRules.ObjectiveLabel(t.Objective)) + "\n" + ClientLanguages.ContentText(view.TargetName)
            + "\n\n" + string.Format(ContractText("Reward: {0} seals"), t.Seals);
        if (t.RewardQuantity > 0) body += "\n" + t.RewardQuantity + " x " + ClientLanguages.ContentText(ContractRewardArt.Name(view));
        mQuestDescLabel.Dock=Pos.None; mQuestDescLabel.SetPosition(0,t.RewardQuantity>0?48:0);
        mQuestDescLabel.ClearText(); mQuestDescLabel.Width = mQuestDescArea.Width - 22; mQuestDescLabel.AddText(body, mQuestDescTemplateLabel); mQuestDescLabel.ForceImmediateRebuild();
        _contractReward ??= new ImagePanel(mQuestDescArea, "ContractReward") { Height = 40, Width = 40, MaintainAspectRatio = true, MouseInputEnabled = false };
        _contractReward.Texture = ContractRewardArt.Texture(t.RewardItemId); _contractReward.IsHidden = _contractReward.Texture == null;
        mQuestDescArea.UpdateScrollBars(); return true;
    }

    private bool UpdateContractTracker()
    {
        var view = SelectedContract;
        if (view == null) return false;
        mQuestTaskHudPanel.Show(); mQuestTaskHudHeader.Text = ContractText("Royal Contracts");
        mQuestTaskHudTitle.Text = ClientLanguages.ContentText(view.Contract.Template.Name);
        var body = ContractText(RoyalContractRules.ObjectiveLabel(view.Contract.Template.Objective)) + "\n" + ClientLanguages.ContentText(view.TargetName);
        if (_lastHudQuestId != view.Contract.Id || _lastHudProgress != view.Progress || _lastHudText != body)
        {
            mQuestTaskHudLabel.ClearText(); mQuestTaskHudLabel.AddText(body, mQuestTaskHudTemplate); mQuestTaskHudLabel.ForceImmediateRebuild();
            _lastHudQuestId = view.Contract.Id; _lastHudProgress = view.Progress; _lastHudText = body;
        }
        mQuestTaskHudProgressLabel.Text = string.Format(ContractText("Progress: {0} / {1}"), view.Progress, view.Contract.Quantity);
        mQuestTaskHudPanel.ShowProgressBar = true; mQuestTaskHudPanel.ProgressRatio = view.Progress / (float)Math.Max(1, view.Contract.Quantity);
        return true;
    }
}

public partial class GameInterface
{
    private ContractCompletionToast? _contractToast;
    private long _journalRefreshAt;
    private Guid _journalPlayer;
    private void UpdateContractJournal()
    {
        if (Intersect.Client.General.Globals.Me == null) return;
        if (_journalPlayer != Intersect.Client.General.Globals.Me.Id)
        {
            _journalPlayer=Intersect.Client.General.Globals.Me.Id;_journalRefreshAt=0;RoyalContractJournal.Reset();
            IntoxicationVisuals.GreenUntil=IntoxicationVisuals.DrunkUntil=0;
        }
        var now = Environment.TickCount64;
        if (now >= _journalRefreshAt)
        {
            _journalRefreshAt = now + 15000;
            Intersect.Client.Networking.Network.SendPacket(new RoyalContractRequestPacket { Action = RoyalContractAction.Journal });
        }
        _contractToast ??= new ContractCompletionToast(GameCanvas);
        _contractToast.UpdateToast(now);
    }
}

internal sealed class ContractCompletionToast : Base
{
    private readonly Label _title, _name;
    private readonly ImagePanel _icon;
    private long _hideAt;
    internal ContractCompletionToast(Canvas parent) : base(parent, nameof(ContractCompletionToast))
    {
        SetSize(440, 92); MouseInputEnabled = KeyboardInputEnabled = false;
        _title = new Label(this) { AutoSizeToContents = false, FontSize = 15, TextColorOverride = new Color(236, 203, 129), MouseInputEnabled = false };
        _title.SetBounds(82, 13, 346, 28);
        _name = new Label(this) { AutoSizeToContents = false, FontSize = 12, TextColorOverride = Color.White, MouseInputEnabled = false, WrappingBehavior = WrappingBehavior.Wrapped };
        _name.SetBounds(82, 43, 346, 42);
        _icon = new ImagePanel(this) { MaintainAspectRatio = true, MouseInputEnabled = false }; _icon.SetBounds(14, 20, 56, 56); Hide();
    }
    internal void UpdateToast(long now)
    {
        if (now >= _hideAt) Hide();
        if (IsHidden && RoyalContractJournal.Completed.TryDequeue(out var view))
        {
            _title.Text = RoyalContractsText.Get("Contract completed!", ClientLanguages.Current);
            _name.Text = ClientLanguages.ContentText(view.Contract.Template.Name);
            _icon.Texture = ContractRewardArt.Texture(view.Contract.Template.RewardItemId);
            _hideAt = now + 6500; Show(); BringToFront();
        }
        SetPosition(Math.Max(0, (Parent.Width - Width) / 2), 100);
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        skin.Renderer.DrawColor = new Color(240, 35, 24, 18); skin.Renderer.DrawFilledRect(RenderBounds);
        skin.Renderer.DrawColor = new Color(236, 203, 129); skin.Renderer.DrawLinedRect(RenderBounds);
    }
}
