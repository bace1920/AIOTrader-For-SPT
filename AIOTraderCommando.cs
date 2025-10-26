using Microsoft.AspNetCore.Razor.TagHelpers;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Dialog.Commando;
using SPTarkov.Server.Core.Helpers.Dialog.Commando.SptCommands;
using SPTarkov.Server.Core.Helpers.Dialogue;
using SPTarkov.Server.Core.Helpers.Dialogue.Commando;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Trade;
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
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Path = System.IO.Path;

namespace BlueheadsAioTrader
{
    /// <summary>
    /// We inject this class into 'AddTraderWithDynamicAssorts' to help us with adding the new trader into the server
    /// </summary>
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
    public class AIOTraderCommando(
        ModHelper modHelper,
        ImageRouter imageRouter,
        ConfigServer configServer,
        TimeUtil timeUtil,
        ISptLogger<AIOTraderCommando> logger, // We are injecting a logger similar to example 1, but notice the class inside <> is different
        DatabaseService databaseService,
        FluentTraderAssortCreator fluentAssortCreator,
        AddCustomTraderHelper addCustomTraderHelper, // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
        MailSendService mailSendService
    ) : ICommandoCommand
    {
        public static string AIO_TRADER_ID = "68f98298939080194f060927";

        private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
        private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();

        public string CommandPrefix { get { return "aio"; } }
        public List<string> Commands => ["give"];

        private Dictionary<string, string> _assortCommandAlias { get; set; } = new()
        {
            ["key"] = "aioKeyCase",
            ["ammo"] = "aioAmmoBox",
            ["dsp"] = "dspTransmitter",
            ["med"] = "aioMedCase",
        };

        private Dictionary<string, MongoId>  _assortContainerIds { get; set; } = new()
        {
            ["aioKeyCase"] = new MongoId(),
            ["aioAmmoBox"] = new MongoId(),
            ["dspTransmitter"] = new MongoId(),
            ["aioMedCase"] = new MongoId(),
        };

        private Dictionary<string, List<Item>> _assortTemplate { get; set; } = new()
        {
            ["aioKeyCase"] = new List<Item>(),
            ["aioAmmoBox"] = new List<Item>(),
            ["dspTransmitter"] = new List<Item>(),
            ["aioMedCase"] = new List<Item>(),
        };
        

        public string GetCommandHelp(string command)
        {
            if (command == "give")
            {
                return "Usage: give [name]\n    key for aio key case\n    ammo for aio ammo box\n    dsp for encoded DSP Transmitter";
            }

            return null;
        }


        public ValueTask<string> Handle(string command, UserDialogInfo commandHandler, MongoId sessionId, SendMessageRequest request)
        {
            var splitCommand = request.Text.Split(" ");
            //logger.Info(request.Text);
            //logger.Info(splitCommand[2]);
            if (_assortTemplate["aioKeyCase"].Count <= 0)
            {
                GenerateAssortTemplate();
            }

            if (command == "give" && new[] { "ammo", "key", "dsp", "med" }.Contains(splitCommand[2]))
            {

                mailSendService.SendDirectNpcMessageToPlayer(
                    sessionId,
                    //AIO_TRADER_ID,
                    "579dc571d53a0658a154fbec",
                    MessageType.MessageWithItems,
                    "I got your package from Bluehead, how do you know this guy?",
                    _assortTemplate[_assortCommandAlias[splitCommand[2]]],
                    172800L
                    );
                logger.Info($"[Bluehead's AioTrader]Total {_assortTemplate[_assortCommandAlias[splitCommand[2]]].Count} item sended.");

            }
            else
            {
                mailSendService.SendUserMessageToPlayer(sessionId, commandHandler, $"Illegal command: {command}");
            }

            return ValueTask.FromResult(request.DialogId);
        }

        private HashSet<MongoId> GetSecureContainerExcludeIds()
        {
            var betaContainer = databaseService.GetItems()["5857a8b324597729ab0a0e7d"];
            return betaContainer.Properties.Grids.ElementAt(0).Properties.Filters.ElementAt(0).ExcludedFilter;
        }

        protected void GenerateAssortTemplate()
        {
            List<Dictionary<string, object>> itemsToSell = new();
            Dictionary<string, List<List<object>>> barterScheme = new();
            Dictionary<string, int> loyaltyLevel = new();

            // default all to aiocase
            foreach (var item in _assortContainerIds) {
                _assortTemplate[item.Key].Add(new Item
                {
                    Id = _assortContainerIds[item.Key],
                    Template = AddAIOCase.AIO_INJECTOR_CASE_ID,
                    ParentId = "5fe49444ae6628187a2e78b8",
                    SlotId = "hideout",
                    Upd = new Upd
                    {
                        StackObjectsCount = 1
                    }
                });
            }

            // overwritten
            _assortTemplate["dspTransmitter"].Add(new Item
            {
                Id = _assortContainerIds["dspTransmitter"],
                Template = ItemTpl.RADIOTRANSMITTER_DIGITAL_SECURE_DSP_RADIO_TRANSMITTER,
                ParentId = "5fe49444ae6628187a2e78b8",
                SlotId = "hideout",
                Upd = new Upd {
                    StackObjectsCount = 1,
                    RecodableComponent = new UpdRecodableComponent {
                        IsEncoded = true
                    }
                }
            });

            AddAllItemsToAssortTemplateByParentId("aioKeyCase", ["5c99f98d86f7745c314214b3", "5c164d2286f774194c5e69fa"], 1);
            AddAllItemsToAssortTemplateByParentId("aioAmmoBox", ["5485a8684bdc2da71d8b4567"], 999999);
            AddAllItemsToAssortTemplateByParentId("aioMedCase", ["5448f3a64bdc2d60728b456a", "5448f3a14bdc2d27728b4569", "5448f39d4bdc2d0a728b4568"], 10);

        }

        protected int AddAllItemsToAssortTemplateByParentId(string assortName, List<string> parentIds, int count)
        {
            var items = databaseService.GetItems();
            int itemCount = 0;
            foreach (var item in items)
            {
                if (!parentIds.Contains(item.Value.Parent.ToString()))
                {
                    continue;
                }
                else if (GetSecureContainerExcludeIds().Contains(item.Value.Id.ToString())) // some items like Rusted bloody key can not put into secure container so we put it out

                {
                    _assortTemplate[assortName].Add(new Item
                    {
                        Id = new MongoId(),
                        Template = item.Value.Id,
                        ParentId = "hideout",
                        SlotId = "hideout",
                        Upd = new Upd
                        {
                            StackObjectsCount = count
                        }
                    });
                }
                else
                {
                    _assortTemplate[assortName].Add(new Item
                    {
                        Id = new MongoId(),
                        Template = item.Value.Id,
                        ParentId = _assortContainerIds[assortName],
                        SlotId = "main",
                        Upd=new Upd
                        {
                            StackObjectsCount=count
                        }
                    });
                }
                itemCount++;
            }
            return itemCount;
        }
    }
}