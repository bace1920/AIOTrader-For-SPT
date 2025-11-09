using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;

using SPTarkov.Server.Core.Models.Utils;
using System.Reflection;

namespace BlueheadsAioTrader;

// We want to load after PreSptModLoader is complete, so we set our type priority to that, plus 1.
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PreSptModLoader + 1)]
public class ReadJsonConfig : IOnLoad // Implement the IOnLoad interface so that this mod can do something
{
    private readonly ISptLogger<ReadJsonConfig> _logger;
    private readonly ModHelper _modHelper;

    public ModConfig config;

    public ReadJsonConfig(
        ISptLogger<ReadJsonConfig> logger,
        ModHelper modHelper)
    {
        _logger = logger;
        _modHelper = modHelper;
    }

    /// <summary>
    /// This is called when this class is loaded, the order in which its loaded is set according to the type priority
    /// on the [Injectable] attribute on this class. Each class can then be used as an entry point to do
    /// things at varying times according to type priority
    /// </summary>
    public Task OnLoad()
    {
        var pathToMod = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        config = _modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "data/config.json");

        if (config.enable_aiotrader == true)
        {
            _logger.Info($"[Bluehead's AioTrader]Config: enable_aiotrader enabled");
        }

        if (config.realistic_price == true)
        {
            _logger.Info($"[Bluehead's AioTrader]Config: realistic_price enabled");
        }

        if (config.hide_no_price_item == true)
        {
            _logger.Info($"[Bluehead's AioTrader]Config: hide_no_price_item enabled");
        }

        if (config.hide_all_ammo_ammopacks == true)
        {
            _logger.Info($"[Bluehead's AioTrader]Config: hide_all_ammo_ammopacks enabled");
        }

        if (config.hide_all_keys_cards == true)
        {
            _logger.Info($"[Bluehead's AioTrader]Config: hide_all_keys_cards enabled");
        }

        if (config.hide_all_builtin_inserts == true)
        {
            _logger.Info($"[Bluehead's AioTrader]Config: hide_all_builtin_inserts enabled");
        }

        if (config.should_aio_trader_assort_on_flea_market == false)
        {
            _logger.Info($"[Bluehead's AioTrader]Config: should_aio_trader_assort_on_flea_market false");
        }

        // Return a completed task
        return Task.CompletedTask;
    }
}

public class ModConfig
{
    public bool enable_aiotrader { get; set; }

    public bool enable_commando_command { get; set; }

    public bool realistic_price { get; set; }

    public double price_modifier { get; set; }

    public bool should_aio_trader_assort_on_flea_market { get; set; }
    
    public Dictionary<string, double> custom_price { get; set; }

    public bool hide_no_price_item { get; set; }

    public bool hide_all_ammo_ammopacks { get; set; }

    public bool hide_all_keys_cards { get; set; }

    public bool hide_all_builtin_inserts { get; set; }
}
