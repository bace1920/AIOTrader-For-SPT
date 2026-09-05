using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;

namespace BlueheadsAioTrader;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 99999 + 1)]
public class PresetAssortService(
    ISptLogger<PresetAssortService> logger,
    DatabaseService databaseService,
    PresetHelper presetHelper,
    ICloner cloner,
    ReadJsonConfig readJsonConfig,
    AddAIOTrader addAIOTrader
) : IOnLoad
{
    private const string AioTraderId = "68f98298939080194f060927";

    public Task OnLoad()
    {
        if (!databaseService.GetTables().Traders.TryGetValue(AioTraderId, out var trader) || trader.Assort == null)
        {
            logger.Warning("[AIOTrader] Trader not found during OnLoad, skipping preset assort build");
            return Task.CompletedTask;
        }

        var assort = cloner.Clone(trader.Assort);
        var defaultPresets = presetHelper.GetDefaultWeaponPresets();
        foreach (var preset in defaultPresets.Values)
        {
            if (preset.Items == null || preset.Items.Count == 0)
                continue;
            if (readJsonConfig.config.hide_no_price_item &&
                preset.Items.Any(item => !addAIOTrader.HasReferencePrice(item.Template)))
                continue;
            var price = readJsonConfig.config.realistic_price
                ? preset.Items.Sum(item => addAIOTrader.GetPrice(item.Template) * (item.Upd?.StackObjectsCount ?? 1))
                : readJsonConfig.config.price_modifier;
            if (!double.IsFinite(price) || price <= 0)
            {
                logger.Warning("[AIOTrader] Skipping preset with invalid total price.");
                continue;
            }
            AddItemListToAssort(assort, preset.Items, price);
        }

        trader.Assort = assort;
        logger.Info($"[AIOTrader] Assort ready: {assort.LoyalLevelItems?.Count} listings ({defaultPresets.Count} default presets)");
        return Task.CompletedTask;
    }

    private static void AddItemListToAssort(TraderAssort assort, List<Item> items, double price)
    {
        var allIds = new HashSet<string>(items.Select(i => i.Id.ToString()));
        var rootItem = items.FirstOrDefault(i => i.ParentId == null || !allIds.Contains(i.ParentId));
        if (rootItem == null)
            return;

        var listingId = new MongoId();

        var idMap = new Dictionary<string, MongoId> { [rootItem.Id.ToString()] = listingId };
        foreach (var part in items.Where(i => i.Id != rootItem.Id))
            idMap[part.Id.ToString()] = new MongoId();

        assort.Items.Add(new Item
        {
            Id = listingId,
            Template = rootItem.Template,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd { UnlimitedCount = true, StackObjectsCount = 999999 }
        });

        foreach (var part in items.Where(i => i.Id != rootItem.Id))
        {
            var mappedParent = part.ParentId != null && idMap.TryGetValue(part.ParentId, out var mp)
                ? mp
                : listingId;

            assort.Items.Add(new Item
            {
                Id = idMap[part.Id.ToString()],
                Template = part.Template,
                ParentId = mappedParent.ToString(),
                SlotId = part.SlotId,
                Upd = part.Upd
            });
        }

        assort.BarterScheme[listingId] = [[new BarterScheme { Count = price, Template = Money.ROUBLES }]];
        assort.LoyalLevelItems[listingId] = 1;
    }
}
