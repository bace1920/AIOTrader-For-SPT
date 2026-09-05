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

    public ModConfig config = new();

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

        config = _modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "data/config.json")
            ?? throw new InvalidDataException("data/config.json must contain a configuration object.");
        config.Validate();

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
    public bool enable_aiotrader { get; set; } = true;

    public bool enable_commando_command { get; set; } = true;

    public bool realistic_price { get; set; }

    public double price_modifier { get; set; } = 1;

    public bool should_aio_trader_assort_on_flea_market { get; set; } = true;
    
    public Dictionary<string, double> custom_price { get; set; } = new();

    public bool hide_no_price_item { get; set; }

    public bool hide_all_ammo_ammopacks { get; set; }

    public bool hide_all_keys_cards { get; set; }

    public bool hide_all_builtin_inserts { get; set; } = true;

    public void Validate()
    {
        custom_price ??= new();
        if (!double.IsFinite(price_modifier) || price_modifier <= 0)
            throw new InvalidDataException("data/config.json: price_modifier must be finite and greater than 0.");

        foreach (var (id, price) in custom_price)
        {
            if (!double.IsFinite(price) || price <= 0 || !double.IsFinite(price * price_modifier))
                throw new InvalidDataException($"data/config.json: custom_price[{id}] must be finite, greater than 0, and remain finite after applying price_modifier.");
        }
    }
}
