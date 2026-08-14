using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace BlueheadsAioTrader;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.bluehead.aiotrader";
    public string Name { get; init; } = "Bluehead's AIO Trader";
    public string Author { get; init; } = "Bluehead";
    public List<string>? Contributors { get; init; } = ["All Users"];
    public SemanticVersioning.Version Version { get; init; } = new("5.3.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://sp-mod.com/mod/374/blueheads-aio-trader";
    public string? License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.GameCallbacks + 1)]
public class Mod : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}