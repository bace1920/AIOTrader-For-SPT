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
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Path = System.IO.Path;

namespace BlueheadsAioTrader
{
    /// <summary>
    /// We inject this class into 'AddTraderWithDynamicAssorts' to help us with adding the new trader into the server
    /// </summary>


    [Injectable(TypePriority = OnLoadOrder.Database + 1)]
    public class AddAIOCase(
        ModHelper modHelper,
        ImageRouter imageRouter,
        ConfigServer configServer,
        TimeUtil timeUtil,
        ISptLogger<Mod> logger, // We are injecting a logger similar to example 1, but notice the class inside <> is different
        DatabaseService databaseService,
        FluentTraderAssortCreator fluentAssortCreator,
        AddCustomTraderHelper addCustomTraderHelper, // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
        CustomItemService customItemService
    ): IOnLoad
    {
        static public string AIO_INJECTOR_CASE_ID = "68f98298939080194f06091e";
        static public string AIO_ITEM_CASE_ID = "68f98298939080194f06091f";
        static public string AIO_AMMO_BOX_ID = "68f98298939080194f060920";

        public Task OnLoad()
        {
            //Example of adding new item by cloning an existing item using `createCloneDetails`
            var exampleCloneItem = new NewItemFromCloneDetails
            {
                ItemTplToClone = ItemTpl.CONTAINER_INJECTOR_CASE,
                // ParentId refers to the Node item the gun will be under, you can check it in https://db.sp-tarkov.com/search
                ParentId = "5795f317245977243854e041",
                // The new id of our cloned item - MUST be a valid mongo id, search online for mongo id generators
                NewId = AIO_INJECTOR_CASE_ID,
                // Flea price of item
                FleaPriceRoubles = 11451419,
                // Price of item in handbook
                HandbookPriceRoubles = 11451419,
                // Handbook Parent Id refers to the category the gun will be under
                HandbookParentId = "5795f317245977243854e041",
                //you see those side box tab thing that only select gun under specific icon? Handbook parent can be found in Spt_Data\Server\database\templates.
                Locales = new Dictionary<string, LocaleDetails>
            {
                {
                    "en", new LocaleDetails
                    {
                        Name = "bluehead's AIO Injector Case",
                        ShortName = "AIO ICase",
                        Description = "bluehead's All In One Injector Case"
                    }
                }
            },
                OverrideProperties = new TemplateItemProperties
                {
                    BackgroundColor = "red",
                    Weight = 0.1,
                    Grids = [
                        new Grid{
                            Name="main",
                            Id="619cbf7d23893217ec30b68b",
                            Parent="619cbf7d23893217ec30b689",
                            Properties=new GridProperties{
                                Filters=[],
                                CellsH=18,
                                CellsV=14,
                                MinCount =0,
                                MaxCount=0,
                                MaxWeight=0,
                                IsSortingTable=false,
                            },
                            Prototype="55d329c24bdc2d892f8b4567"
                        }
                    ]
                },
            };

            var createItemResult = customItemService.CreateItemFromClone(exampleCloneItem); // Send our data to the function that creates our item
                                                                                            //logger.Info(createItemResult.ToString());
            ModifyContainerFilter();

            return Task.CompletedTask;
        }

        private void ModifyContainerFilter()
        {
            MongoId contrainerParentId = new MongoId("5448bf274bdc2dfc2f8b456a");
            var items = databaseService.GetItems();

            foreach (var item in items)
            {
                if (item.Value.Parent == contrainerParentId.ToString())
                {
                    item.Value.Properties.Grids.ElementAt(0).Properties.Filters.ElementAt(0).Filter.Add(contrainerParentId);
                }
            }
        }
    }
}
