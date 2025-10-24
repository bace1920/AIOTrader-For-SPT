using SPTarkov.DI.Annotations;
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
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Path = System.IO.Path;


namespace BlueheadsAioTrader;

// This record holds the various properties for your mod
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.bluehead.aiotrader";
    public override string Name { get; init; } = "Bluehead's AIO Trader";
    public override string Author { get; init; } = "Bluehead";
    public override List<string>? Contributors { get; init; } = ["All Users"];
    public override SemanticVersioning.Version Version { get; init; } = new("5.0.1");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; } = [];
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://forge.sp-tarkov.com/mod/374/blueheads-aio-trader";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}

/// <summary>
/// Feel free to use this as a base for your mod
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class Mod(
    ModHelper modHelper,
    ImageRouter imageRouter,
    ConfigServer configServer,
    TimeUtil timeUtil,
    ISptLogger<Mod> logger, // We are injecting a logger similar to example 1, but notice the class inside <> is different
    DatabaseService databaseService,
    FluentTraderAssortCreator fluentAssortCreator,
    AddCustomTraderHelper addCustomTraderHelper, // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
    AddAIOTrader addAIOTrader,
    AddAIOCase addAIOCase
)
    : IOnLoad
{
    private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
    private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();


    public Task OnLoad()
    {
        return Task.CompletedTask;
    }

    
}