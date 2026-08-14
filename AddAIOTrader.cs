using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using System.Collections.Generic;
using System.Reflection;
using Path = System.IO.Path;

namespace BlueheadsAioTrader
{
    [Injectable(TypePriority = OnLoadOrder.TraderRegistration + 99999)]
    public class AddAIOTrader(
        ModHelper modHelper,
        ImageRouter imageRouter,
        ISptLogger<AddAIOTrader> logger,
        TemplateTable templateTable,
        FluentTraderAssortCreator fluentAssortCreator,
        AddCustomTraderHelper addCustomTraderHelper,
        ReadJsonConfig readJsonConfig
    ) : IOnLoad
    {
        public Task OnLoadAsync(CancellationToken cancellationToken = default)
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
            var items = templateTable.Items;

            foreach (var item in items)
            {
                if (ShouldBehidden(item.Value) == true)
                {
                    continue;
                }

                CreateSingleAssort(item.Value);

                count++;
            }

            logger.Info("[Bluehead's AioTrader]Total " + count + " items loaded");
            fluentAssortCreator.Dump(dumpPath);
            fluentAssortCreator.Clear();
        }

        private void InsertLockedPlate(MongoId uuid, TemplateItem item)
        {
            if (item.Properties?.Slots == null) return;
            foreach (var slot in item.Properties.Slots)
            {
                var filter = slot.Properties?.Filters?.FirstOrDefault();
                if (filter?.Locked == true && filter.Plate != null)
                    fluentAssortCreator.AddSlotItem(uuid, filter.Plate, slot.Name);
            }
        }

        private void InsertAmmoPack(MongoId uuid, TemplateItem item)
        {
            var stackSlot = item.Properties?.StackSlots?.FirstOrDefault();
            var ammoId = stackSlot?.Properties?.Filters?.FirstOrDefault()?.Filter?.FirstOrDefault();
            if (ammoId == null) return;
            var stackCount = stackSlot!.MaxCount;
            fluentAssortCreator.AddSlotItem(uuid, ammoId, "cartridges", stackCount);
        }

        private void CreateSingleAssort(TemplateItem item)
        {
            var tempId = new MongoId();

            fluentAssortCreator.CreateSingleAssortItem(item, tempId, price: GetPrice(item));
            if (item.Parent == "5448e54d4bdc2dcc718b4568" // armor
                || item.Parent == "5a341c4086f77401f2541505" // helmet
                || item.Parent == "5448e5284bdc2dcb718b4567") // vest
            {
                this.InsertLockedPlate(tempId, item);
            }
            else if (item.Parent == "543be5cb4bdc2deb348b4568") // pack of ammo
            {
                this.InsertAmmoPack(tempId, item);
            }
        }

        private readonly List<string> ammo_ammopacks_parent_ids = new List<string> { "5485a8684bdc2da71d8b4567", "543be5cb4bdc2deb348b4568" };
        private readonly List<string> keys_cards_parent_ids = new List<string> { "5c99f98d86f7745c314214b3", "5c164d2286f774194c5e69fa" };
        private readonly List<string> builtin_inserts_plates_parent_ids = new List<string> { "65649eb40bf0ed77b8044453" };

        private bool ShouldBehidden(TemplateItem item)
        {
            if (item.Type != "Item")
                return true;

            if (readJsonConfig.config.hide_all_ammo_ammopacks &&
                ammo_ammopacks_parent_ids.Contains(item.Parent.ToString()))
                return true;

            if (readJsonConfig.config.hide_all_keys_cards &&
                keys_cards_parent_ids.Contains(item.Parent.ToString()))
                return true;

            if (readJsonConfig.config.hide_all_builtin_inserts &&
                builtin_inserts_plates_parent_ids.Contains(item.Parent.ToString()))
                return true;

            if (readJsonConfig.config.hide_no_price_item &&
                !readJsonConfig.config.custom_price.ContainsKey(item.Id) &&
                !templateTable.Prices.ContainsKey(item.Id))
                return true;

            return false;
        }

        public double GetPrice(TemplateItem item)
        {
            double price;
            var prices = templateTable.Prices;
            
            if (readJsonConfig.config.custom_price.TryGetValue(item.Id, out price))
            {
                return price * readJsonConfig.config.price_modifier;
            }
            if (readJsonConfig.config.realistic_price == true && prices.TryGetValue(item.Id, out price))
            {
                return price * readJsonConfig.config.price_modifier;
            }

            return 1 * readJsonConfig.config.price_modifier;
        }
    }


}
