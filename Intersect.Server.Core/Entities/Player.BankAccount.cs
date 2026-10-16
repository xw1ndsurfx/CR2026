using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.LogiCoins;
using Intersect.Server.Networking;
using Newtonsoft.Json;
namespace Intersect.Server.Entities;

public sealed class BankAccountProfile
{
    public bool Opened { get; set; }
    public int BaseSlots { get; set; }
    public bool PremiumGranted { get; set; }
}
public partial class Player
{
    [JsonIgnore, Column("BankAccount")] public string BankAccountJson { get => JsonConvert.SerializeObject(BankAccount); set => BankAccount = string.IsNullOrWhiteSpace(value) ? new() : JsonConvert.DeserializeObject<BankAccountProfile>(value) ?? new(); }
    [JsonIgnore, NotMapped] public BankAccountProfile BankAccount { get; set; } = new();
    [JsonIgnore, NotMapped] public bool HasBankPremium => User != null && LogiCoinPurchaseRuntime.HasActivePremium(this);
    [JsonIgnore, NotMapped] public int BaseBankSlots => BankAccount.BaseSlots > 0 ? BankAccount.BaseSlots : Math.Max(Options.Instance.Player.InitialBankslots, Bank.Capacity);
    internal int EnsureBankCapacity()
    {
        if (BankAccount.BaseSlots <= 0) BankAccount.BaseSlots = BaseBankSlots;
        if (HasBankPremium) BankAccount.PremiumGranted = true;
        var capacity = BankAccount.BaseSlots * (BankAccount.PremiumGranted ? 2 : 1);
        var storageCapacity = Math.Max(Bank.Count, capacity);
        if (Bank.Capacity != storageCapacity) Bank.Capacity = storageCapacity;
        return HasBankPremium ? BankAccount.BaseSlots * 2 : BankAccount.BaseSlots;
    }
    private bool EnsureBankAccess()
    {
        var config = Intersect.Server.WorldEvents.WorldActivitiesRuntime.Current;
        if (Level < config.BankRequiredLevel) { BankNotice("Bank access requires level {0}.", config.BankRequiredLevel); return false; }
        if (BankAccount.Opened) { EnsureBankCapacity(); return true; }
        var old = BankAccountJson;
        var oldCapacity = Bank.Capacity;
        var inventory = Items.Select(i => i.Clone()).ToArray();
        // Existing stored goods remain accessible without retroactively charging an opening fee.
        var legacy = BankAccount.BaseSlots == 0 && Bank.Any(s => s.ItemId != Guid.Empty && s.Quantity > 0);
        var currency = config.BankOpeningCurrencyId != Guid.Empty ? ItemDescriptor.Get(config.BankOpeningCurrencyId) : ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().FirstOrDefault(i => i.Name.Equals("Auragems", StringComparison.OrdinalIgnoreCase) || i.Name.Equals("Auragem", StringComparison.OrdinalIgnoreCase));
        if (!legacy && config.BankOpeningCost > 0 && (currency == null || !TryTakeItem(currency.Id, config.BankOpeningCost, sendUpdate: false)))
        { BankNotice("Opening a bank account costs {0} {1}.", config.BankOpeningCost, currency?.Name ?? "Auragems"); return false; }
        BankAccount.Opened = true; EnsureBankCapacity();
        if (User?.Save(force: true) != UserSaveResult.Completed)
        {
            BankAccountJson = old; Bank.Capacity = Math.Max(Bank.Count, oldCapacity); for (var i = 0; i < inventory.Length; i++) Items[i].Set(inventory[i]); PacketSender.SendInventory(this); BankNotice("Your bank account could not be saved. Please try again."); return false;
        }
        PacketSender.SendInventory(this); return true;
    }
    private void BankNotice(string text, params object[] args)
    {
        // The client interpolates the English catalog template into the selected language.
        PacketSender.SendChatMsg(this, args.Length == 0 ? text : string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args), Intersect.Enums.ChatMessageType.Bank);
    }
}
