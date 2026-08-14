using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.Modding.Custom;
using System.Reflection;

namespace BlueheadsAioTrader
{
    [Injectable(TypePriority = OnLoadOrder.GameCallbacks + 1)]
    public class AddAIOCase(
        CustomItemService customItemService
    ): IOnLoad
    {
        public const string AIO_INJECTOR_CASE_ID = "68f98298939080194f06091e";

        public Task OnLoadAsync(CancellationToken cancellationToken = default)
        {
            var cloneDetails = new NewItemFromCloneDetails
            {
                ItemTplToClone = ItemTpl.CONTAINER_INJECTOR_CASE,
                NewItemName = "bluehead's AIO Injector Case",
                ParentId = "5795f317245977243854e041",
                NewId = AIO_INJECTOR_CASE_ID,
                FleaPriceRoubles = 11451419,
                HandbookPriceRoubles = 11451419,
                HandbookParentId = "5795f317245977243854e041",
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
                        new Grid
                        {
                            Name = "main",
                            Id = "619cbf7d23893217ec30b68b",
                            Parent = "619cbf7d23893217ec30b689",
                            Properties = new GridProperties
                            {
                                Filters = [],
                                CellsH = 18,
                                CellsV = 14,
                                MinCount = 0,
                                MaxCount = 0,
                                MaxWeight = 0,
                                IsSortingTable = false,
                            },
                            Prototype = "55d329c24bdc2d892f8b4567"
                        }
                    ]
                },
            };

            customItemService.CreateItemFromClone(cloneDetails, Assembly.GetExecutingAssembly());
            return Task.CompletedTask;
        }
    }
}

