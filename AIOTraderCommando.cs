using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Dialogue.Commando;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;

namespace BlueheadsAioTrader
{
    [Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.GameCallbacks + 2)]
    public class AIOTraderCommando(
        ISptLogger<AIOTraderCommando> logger,
        TemplateTable templateTable,
        ReadJsonConfig readJsonConfig,
        MailSendService mailSendService
    ) : ICommandoCommand, IOnLoad
    {
        private const string AIO_TRADER_ID = "68f98298939080194f060927";

        public string CommandPrefix { get { return "aio"; } }
        public List<string> Commands => ["give"];

        public Task OnLoadAsync(CancellationToken cancellationToken = default)
        {
            logger.Info("[Bluehead's AioTrader] AIOTraderCommando loaded — prefix: 'aio', commands: give");
            return Task.CompletedTask;
        }

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
                return "Usage: give [name]\n    key for aio key case\n    ammo for aio ammo box\n    dsp for encoded DSP Transmitter";
            return null;
        }


        public ValueTask<string> Handle(string command, UserDialogInfo commandHandler, MongoId sessionId, SendMessageRequest request)
        {
            if (!readJsonConfig.config.enable_commando_command)
                return ValueTask.FromResult(request.DialogId);

            var splitCommand = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var aliasArg = splitCommand.FirstOrDefault(p => _assortCommandAlias.ContainsKey(p));

            if (aliasArg == null)
            {
                mailSendService.SendUserMessageToPlayer(sessionId, commandHandler, GetCommandHelp(command));
                return ValueTask.FromResult(request.DialogId);
            }

            if (_assortTemplate["aioKeyCase"].Count <= 0)
                GenerateAssortTemplate();

            if (command == "give")
            {
                mailSendService.SendDirectNpcMessageToPlayer(
                    sessionId,
                    "579dc571d53a0658a154fbec",
                    MessageType.MessageWithItems,
                    "I got your package from Bluehead, how do you know this guy?",
                    _assortTemplate[_assortCommandAlias[aliasArg]],
                    172800L
                );
                logger.Info($"[Bluehead's AioTrader]Total {_assortTemplate[_assortCommandAlias[aliasArg]].Count} item sended.");
            }
            else
            {
                mailSendService.SendUserMessageToPlayer(sessionId, commandHandler, $"Illegal command: {command}");
            }

            return ValueTask.FromResult(request.DialogId);
        }

        private HashSet<MongoId> GetSecureContainerExcludeIds()
        {
            var betaContainer = templateTable.Items["5857a8b324597729ab0a0e7d"];
            return betaContainer.Properties.Grids.ElementAt(0).Properties.Filters.ElementAt(0).ExcludedFilter;
        }

        protected void GenerateAssortTemplate()
        {
            // default key/ammo/med templates: root is the AIO injector case container
            foreach (var key in _assortContainerIds.Keys.Where(k => k != "dspTransmitter"))
            {
                _assortTemplate[key].Add(new Item
                {
                    Id = _assortContainerIds[key],
                    Template = AddAIOCase.AIO_INJECTOR_CASE_ID,
                    ParentId = "5fe49444ae6628187a2e78b8",
                    SlotId = "hideout",
                    Upd = new Upd { StackObjectsCount = 1 }
                });
            }

            // dspTransmitter is a standalone item, not inside a container
            _assortTemplate["dspTransmitter"].Add(new Item
            {
                Id = _assortContainerIds["dspTransmitter"],
                Template = ItemTpl.RADIOTRANSMITTER_DIGITAL_SECURE_DSP_RADIO_TRANSMITTER,
                ParentId = "5fe49444ae6628187a2e78b8",
                SlotId = "hideout",
                Upd = new Upd
                {
                    StackObjectsCount = 1,
                    RecodableComponent = new UpdRecodableComponent { IsEncoded = true }
                }
            });

            AddAllItemsToAssortTemplateByParentId("aioKeyCase", ["5c99f98d86f7745c314214b3", "5c164d2286f774194c5e69fa"], 1);
            AddAllItemsToAssortTemplateByParentId("aioAmmoBox", ["5485a8684bdc2da71d8b4567"], 999999);
            AddAllItemsToAssortTemplateByParentId("aioMedCase", ["5448f3a64bdc2d60728b456a", "5448f3a14bdc2d27728b4569", "5448f39d4bdc2d0a728b4568"], 10);

        }

        protected int AddAllItemsToAssortTemplateByParentId(string assortName, List<string> parentIds, int count)
        {
            var items = templateTable.Items;
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