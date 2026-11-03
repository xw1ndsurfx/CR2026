using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Housing;
using Intersect.Network.Packets.Housing;

namespace Intersect.Client.Interface.Game;

internal sealed partial class HousingWindow
{
    private void RefreshFarmRows()
    {
        if (_view == null) return;
        var farm = _view.House.Farm;
        var config = _view.FarmConfiguration;
        var y = 4;
        void Line(string text)
        {
            var label = new Label(_rows) { Text = text, Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11, TextColorOverride = Color.White };
            label.SetBounds(8, y, 770, 30); y += 34;
        }
        void Action(string text, Action click, int x = 8, int width = 240) => AddButton(_rows, text, x, y, width, click);
        string ItemName(Guid id) => ClientLanguages.ContentText(ItemDescriptor.Get(id)?.Name ?? id.ToString());
        if (!_view.IsOwner) { Line(ClientLanguages.Text("Only the owner can manage this farm.")); return; }
        if (!config.Enabled || !_view.Enabled) { Line(ClientLanguages.Text("Farming is not available yet.")); return; }
        Line(ClientLanguages.Format("Farmer level {0} - EXP {1}", FarmingProfession.Definition.LevelForExperience(farm.Experience), farm.Experience));
        Line(ClientLanguages.Format("Salary fund: {0} {1} - granary: {2}/{3}", farm.Wallet, ItemName(farm.Wallet > 0 ? farm.CurrencyItemId : config.CurrencyItemId), farm.Storage.Values.Sum(), config.StorageCapacity));
        Line(ClientLanguages.Format("Harvested: {0} - wages paid: {1}", farm.Harvested, farm.WagesPaid));
        Action("Visit farm", () => PacketSender.SendHouseAction(HouseAction.VisitFarm, _view.OwnerId)); y += 38;
        Action("Deposit 100 for salaries", () => PacketSender.SendHouseAction(HouseAction.DepositFarmSalary, _view.OwnerId, x: 100));
        if (farm.Wallet > 0) Action("Withdraw salary fund", () => PacketSender.SendHouseAction(HouseAction.WithdrawFarmSalary, _view.OwnerId, x: farm.Wallet), 270);
        y += 38;
        if (farm.Plots.Count < config.MaximumWorkers)
        {
            Action(ClientLanguages.Format("Hire novice ({0})", config.HiringPrice), () => PacketSender.SendHouseAction(HouseAction.HireFarmWorker, _view.OwnerId), width: 365);
            Action(ClientLanguages.Format("Hire expert ({0})", config.HiringPrice * 2), () => PacketSender.SendHouseAction(HouseAction.HireFarmWorker, _view.OwnerId, rotation: 1), 390, 365); y += 38;
        }
        foreach (var crop in config.Crops.DistinctBy(c => c.SeedItemId))
        {
            Line(ClientLanguages.Format("{0}: {1} seeds", ItemName(crop.SeedItemId), farm.Seeds.GetValueOrDefault(crop.SeedItemId)));
            Action("Deposit 10 seeds", () => PacketSender.SendHouseAction(HouseAction.DepositFarmSeeds, _view.OwnerId, crop.SeedItemId, x: 10)); y += 38;
        }
        foreach (var (plot, index) in farm.Plots.Select((p, i) => (p, i)))
        {
            Line(ClientLanguages.Format("Worker {0} ({1}) - {2}", index + 1, ClientLanguages.Text(plot.Experienced ? "Expert" : "Novice"), ClientLanguages.Text(farm.Status(plot, config))));
            var selected = config.Crops.FirstOrDefault(c => c.Id == plot.CropId);
            Line(ClientLanguages.Format("Crop: {0} - wage per cycle: {1}", selected == null ? ClientLanguages.Text("Choose a crop") : ClientLanguages.ContentText(ClientLanguages.Text(selected.Name)), config.WagePerCycle * (plot.Experienced ? 2 : 1)));
            Action("Pause after harvest", () => PacketSender.SendHouseAction(HouseAction.PauseFarmPlot, _view.OwnerId, plot.Id));
            if (plot.DueUtc == 0) Action("Dismiss worker", () => PacketSender.SendHouseAction(HouseAction.DismissFarmWorker, _view.OwnerId, plot.Id), 270);
            y += 38;
            foreach (var crop in config.Crops)
            {
                var caption = ClientLanguages.Format("Grow {0} (level {1}, {2} min)", ClientLanguages.ContentText(ClientLanguages.Text(crop.Name)), crop.RequiredLevel, crop.Seconds / 60);
                var button = AddButton(_rows, caption, 8, y, 745, () => PacketSender.SendHouseAction(HouseAction.SetFarmCrop, _view.OwnerId, plot.Id, crop.Id));
                button.IsDisabled = FarmingProfession.Definition.LevelForExperience(farm.Experience) < crop.RequiredLevel;
                y += 38;
            }
        }
        foreach (var entry in farm.Storage.Where(e => e.Value > 0))
        {
            Line(ClientLanguages.Format("{0}: {1}", ItemName(entry.Key), entry.Value));
            Action("Collect harvest", () => PacketSender.SendHouseAction(HouseAction.CollectFarmHarvest, _view.OwnerId, entry.Key)); y += 38;
        }
        _rows.SetInnerSize(_rows.Width - 20, y + 10);
    }
}
