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
        private const int DEFAULT_AMMO_STACKS = 10;
        private const int MAX_AMMO_STACKS = 100;

        // grenade-launcher calibers: StackMaxSize=1, cannot stack, excluded from aioAmmoBox
        private static readonly HashSet<string> GrenadeCalibres = ["Caliber40mmRU", "Caliber40x46", "Caliber30x29"];

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

        private Dictionary<string, MongoId> _assortContainerIds { get; set; } = new()
        {
            ["aioKeyCase"] = new MongoId(),
            ["dspTransmitter"] = new MongoId(),
            ["aioMedCase"] = new MongoId(),
        };

        // aioAmmoBox: one entry per stackable ammo type (no container wrapper, just the ammo item)
        // aioNadeBox: one entry per grenade-launcher round (StackMaxSize=1, delivered loose)
        private Dictionary<string, List<Item>> _assortTemplate { get; set; } = new()
        {
            ["aioKeyCase"] = new List<Item>(),
            ["aioAmmoBox"] = new List<Item>(),
            ["aioNadeBox"] = new List<Item>(),
            ["dspTransmitter"] = new List<Item>(),
            ["aioMedCase"] = new List<Item>(),
        };
        

        public string GetCommandHelp(string command)
        {
            if (command == "give")
                return "Usage: give [name] [count]\n    key          — aio key case\n    ammo [n]     — ammo box (each ammo type gets its own sub-box, n sub-boxes, default 10, max 100)\n    dsp          — encoded DSP Transmitter\n    med          — aio med case";
            return null;
        }

        public ValueTask<string> Handle(string command, UserDialogInfo commandHandler, MongoId sessionId, SendMessageRequest request)
        {
            if (!readJsonConfig.config.enable_commando_command)
                return ValueTask.FromResult(request.DialogId);

            var splitCommand = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var aliasArg = splitCommand.FirstOrDefault(_assortCommandAlias.ContainsKey);

            if (aliasArg == null)
            {
                mailSendService.SendUserMessageToPlayer(sessionId, commandHandler, GetCommandHelp(command));
                return ValueTask.FromResult(request.DialogId);
            }

            if (_assortTemplate["aioKeyCase"].Count <= 0)
                GenerateAssortTemplate();

            if (command == "give")
            {
                List<Item> items;
                if (aliasArg == "ammo")
                {
                    var stacksArg = splitCommand.FirstOrDefault(p => int.TryParse(p, out _));
                    var copies = stacksArg != null ? Math.Clamp(int.Parse(stacksArg), 1, MAX_AMMO_STACKS) : DEFAULT_AMMO_STACKS;
                    items = BuildAmmoItems(_assortTemplate["aioAmmoBox"], _assortTemplate["aioNadeBox"], copies);
                }
                else
                {
                    items = _assortTemplate[_assortCommandAlias[aliasArg]];
                }

                mailSendService.SendDirectNpcMessageToPlayer(
                    sessionId,
                    "579dc571d53a0658a154fbec",
                    MessageType.MessageWithItems,
                    "I got your package from Bluehead, how do you know this guy?",
                    items,
                    172800L
                );
                logger.Info($"[Bluehead's AioTrader] Sent {items.Count} items (command: {aliasArg}).");
            }
            else
            {
                mailSendService.SendUserMessageToPlayer(sessionId, commandHandler, $"Illegal command: {command}");
            }

            return ValueTask.FromResult(request.DialogId);
        }

        // ammoEntries: one item per stackable ammo type (StackMaxSize qty cached)
        // nadeEntries: one item per grenade-launcher round (qty 1)
        // copies: how many A-boxes to create; each A-box contains every ammo type once at StackMaxSize
        // Result: 1 outer box → copies A-boxes (each with all ammo types) + grenade rounds loose
        private static List<Item> BuildAmmoItems(List<Item> ammoEntries, List<Item> nadeEntries, int copies)
        {
            var outerId = new MongoId();
            var result = new List<Item>
            {
                new()
                {
                    Id = outerId,
                    Template = AddAIOCase.AIO_INJECTOR_CASE_ID,
                    ParentId = "5fe49444ae6628187a2e78b8",
                    SlotId = "hideout",
                    Upd = new Upd { StackObjectsCount = 1 }
                }
            };

            // n A-boxes, each containing all stackable ammo types
            for (int i = 0; i < copies; i++)
            {
                var subId = new MongoId();
                result.Add(new Item
                {
                    Id = subId,
                    Template = AddAIOCase.AIO_INJECTOR_CASE_ID,
                    ParentId = outerId,
                    SlotId = "main",
                    Upd = new Upd { StackObjectsCount = 1 }
                });
                foreach (var src in ammoEntries)
                {
                    result.Add(new Item
                    {
                        Id = new MongoId(),
                        Template = src.Template,
                        ParentId = subId,
                        SlotId = "main",
                        Upd = new Upd { StackObjectsCount = src.Upd?.StackObjectsCount ?? 1 }
                    });
                }
            }

            // grenade rounds loose in the outer box, 1 each
            foreach (var src in nadeEntries)
            {
                result.Add(new Item
                {
                    Id = new MongoId(),
                    Template = src.Template,
                    ParentId = outerId,
                    SlotId = "main",
                    Upd = new Upd { StackObjectsCount = 1 }
                });
            }

            return result;
        }

        private HashSet<MongoId> GetSecureContainerExcludeIds()
        {
            var betaContainer = templateTable.Items["5857a8b324597729ab0a0e7d"];
            return betaContainer.Properties.Grids.ElementAt(0).Properties.Filters.ElementAt(0).ExcludedFilter;
        }

        protected void GenerateAssortTemplate()
        {
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
            CacheAmmoEntries("aioAmmoBox", "aioNadeBox");
            AddAllItemsToAssortTemplateByParentId("aioMedCase", ["5448f3a64bdc2d60728b456a", "5448f3a14bdc2d27728b4569", "5448f39d4bdc2d0a728b4568"], 10);

        }

        protected int AddAllItemsToAssortTemplateByParentId(string assortName, List<string> parentIds, int count, HashSet<string>? excludeCalibres = null)
        {
            var items = templateTable.Items;
            int itemCount = 0;
            foreach (var item in items)
            {
                if (!parentIds.Contains(item.Value.Parent.ToString()))
                    continue;

                if (excludeCalibres != null && excludeCalibres.Contains(item.Value.Properties?.Caliber ?? ""))
                    continue;

                var stackCount = count > 0 ? count : (item.Value.Properties?.StackMaxSize ?? 1);

                if (GetSecureContainerExcludeIds().Contains(item.Value.Id.ToString()))
                {
                    _assortTemplate[assortName].Add(new Item
                    {
                        Id = new MongoId(),
                        Template = item.Value.Id,
                        ParentId = "hideout",
                        SlotId = "hideout",
                        Upd = new Upd { StackObjectsCount = stackCount }
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
                        Upd = new Upd { StackObjectsCount = stackCount }
                    });
                }
                itemCount++;
            }
            return itemCount;
        }

        // Populates aioAmmoBox (one entry per stackable ammo, StackMaxSize qty)
        // and aioNadeBox (one entry per grenade-launcher round, qty 1)
        protected void CacheAmmoEntries(string ammoKey, string nadeKey)
        {
            var ammoParent = "5485a8684bdc2da71d8b4567";
            foreach (var item in templateTable.Items.Values)
            {
                if (item.Parent.ToString() != ammoParent)
                    continue;

                bool isGren = GrenadeCalibres.Contains(item.Properties?.Caliber ?? "");
                var qty = isGren ? 1 : (item.Properties?.StackMaxSize ?? 1);
                var key = isGren ? nadeKey : ammoKey;

                _assortTemplate[key].Add(new Item
                {
                    Id = new MongoId(),
                    Template = item.Id,
                    ParentId = "placeholder",
                    SlotId = "main",
                    Upd = new Upd { StackObjectsCount = qty }
                });
            }
        }
    }
}