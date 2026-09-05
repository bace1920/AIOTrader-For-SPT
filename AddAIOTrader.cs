using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using Path = System.IO.Path;

namespace BlueheadsAioTrader
{
    // make it will be the last in PostDBModLoader
    [Injectable(TypePriority = OnLoadOrder.TraderRegistration + 99999)]


    public class AddAIOTrader(
        ModHelper modHelper,
        ImageRouter imageRouter,
        ConfigServer configServer,
        TimeUtil timeUtil,
        ISptLogger<Mod> logger, // We are injecting a logger similar to example 1, but notice the class inside <> is different
        DatabaseService databaseService,
        FluentTraderAssortCreator fluentAssortCreator,
        AddCustomTraderHelper addCustomTraderHelper, // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
        ReadJsonConfig readJsonConfig
    ) : IOnLoad
    {
        private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
        private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();

        public Task OnLoad()
        {
            if (readJsonConfig.config.enable_aiotrader == false)
            {
                return Task.CompletedTask; 
            }

            // A path to the mods files we use below
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

            GenerateAssorts(Path.Combine(pathToMod, "data/assort_aio_trader.json"));
            addCustomTraderHelper.RegisterCustomTrader(
                "data/trader_aio_trader.json",
                "data/aiotrader.png",
                "data/assort_aio_trader.json",
                "AioTrader",
                "This is the AioTrader.",
                readJsonConfig.config.should_aio_trader_assort_on_flea_market
                );

            return Task.CompletedTask;
        }

        private void GenerateAssorts(string dumpPath)
        {
            var count = 0;
            var items = databaseService.GetItems();

            foreach (var item in items)
            {
                if (ShouldBehidden(item.Value) == true)
                {
                    continue;
                }

                if (CreateSingleAssort(item.Value))
                    count++;
            }

            logger.Info("[Bluehead's AioTrader]Total " + count + " items loaded");
            fluentAssortCreator.Dump(dumpPath);
            fluentAssortCreator.Clear();
        }

        private void InsertLockedPlate(MongoId uuid, TemplateItem item)
        {
            foreach (var slot in item.Properties?.Slots ?? [])
            {
                var filters = slot?.Properties?.Filters;
                if (filters == null || !filters.Any())
                {
                    logger.Warning($"[AIOTrader] Item {item.Id}: slot {slot?.Name} has no filters; skipping slot.");
                    continue;
                }
                foreach (var filter in filters.Where(filter => filter?.Locked == true))
                {
                    if (filter.Plate is not { } plate || plate.IsEmpty ||
                        !databaseService.GetItems().ContainsKey(plate) || string.IsNullOrWhiteSpace(slot?.Name))
                    {
                        logger.Warning($"[AIOTrader] Item {item.Id}: invalid locked plate in slot {slot?.Name}; skipping slot.");
                        continue;
                    }
                    fluentAssortCreator.AddSlotItem(uuid, plate, slot.Name);
                    break;
                }
            }
        }

        internal static bool TryGetAmmoPackContents(TemplateItem item, out MongoId ammoId, out double count)
        {
            ammoId = default;
            count = 0;
            var stackSlot = item.Properties?.StackSlots?.FirstOrDefault();
            var filter = stackSlot?.Properties?.Filters?.FirstOrDefault()?.Filter;
            if (filter == null || !filter.Any() || stackSlot?.MaxCount is not { } quantity ||
                !double.IsFinite(quantity) || quantity <= 0 || quantity != Math.Floor(quantity))
                return false;
            ammoId = filter.First();
            count = quantity;
            return !ammoId.IsEmpty;
        }

        private bool CreateSingleAssort(TemplateItem item)
        {
            var tempId = new MongoId();
            var isAmmoPack = item.Parent == "543be5cb4bdc2deb348b4568";
            MongoId ammoId = default;
            double stackCount = 0;
            if (item.Properties == null || (isAmmoPack &&
                (!TryGetAmmoPackContents(item, out ammoId, out stackCount) ||
                 !databaseService.GetItems().ContainsKey(ammoId))))
            {
                logger.Warning($"[AIOTrader] Skipping item {item.Id}: missing properties or invalid ammo-pack contents.");
                return false;
            }

            fluentAssortCreator.CreateSingleAssortItem(item, tempId, price: GetPrice(item));
            if (item.Parent == "5448e54d4bdc2dcc718b4568" // armor
                || item.Parent == "5a341c4086f77401f2541505" // helmet
                || item.Parent == "5448e5284bdc2dcb718b4567") // vest
            {
                InsertLockedPlate(tempId, item);
            }
            else if (isAmmoPack)
            {
                fluentAssortCreator.AddSlotItem(tempId, ammoId, "cartridges", stackCount);
            }
            return true;
        }

        private readonly List<string> ammo_ammopacks_parent_ids = new List<string> { "5485a8684bdc2da71d8b4567", "543be5cb4bdc2deb348b4568" };
        private readonly List<string> keys_cards_parent_ids = new List<string> { "5c99f98d86f7745c314214b3", "5c164d2286f774194c5e69fa" };
        private readonly List<string> builtin_inserts_plates_parent_ids = new List<string> { "65649eb40bf0ed77b8044453" };

        private bool ShouldBehidden(TemplateItem item)
        {
            if (item.Type != "Item")
            {
                return true;
            }
            // Reference-price availability is independent of the realistic_price switch.
            if (readJsonConfig.config.hide_no_price_item && !HasReferencePrice(item.Id))
            {
                return true;
            }
            //logger.Info(item.Type.GetType().ToString());
            if (readJsonConfig.config.hide_all_ammo_ammopacks == true &&
                ammo_ammopacks_parent_ids.Contains(item.Parent.ToString()))
            {
                return true;
            }
            else if (readJsonConfig.config.hide_all_keys_cards == true &&
                keys_cards_parent_ids.Contains(item.Parent.ToString()))
            {
                return true;
            }
            else if (readJsonConfig.config.hide_all_builtin_inserts == true &&
                builtin_inserts_plates_parent_ids.Contains(item.Parent.ToString()))
            {
                return true;
            }
            return false;
        }

        public bool HasReferencePrice(MongoId templateId)
        {
            return readJsonConfig.config.custom_price.ContainsKey(templateId)
                || (databaseService.GetPrices().TryGetValue(templateId, out var price)
                    && double.IsFinite(price) && price > 0);
        }

        public double GetPrice(TemplateItem item) => GetPrice(item.Id);

        public double GetPrice(MongoId templateId)
        {
            double price;
            var prices = databaseService.GetPrices();
            
            if (readJsonConfig.config.custom_price.TryGetValue(templateId, out price))
            {
                return price * readJsonConfig.config.price_modifier;
            }
            if (readJsonConfig.config.realistic_price == true && prices.TryGetValue(templateId, out price)
                && double.IsFinite(price) && price > 0 && double.IsFinite(price * readJsonConfig.config.price_modifier))
            {
                return price * readJsonConfig.config.price_modifier;
            }

            return 1 * readJsonConfig.config.price_modifier;
        }
    }


}
