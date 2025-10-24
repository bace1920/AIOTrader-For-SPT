using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BlueheadsAioTrader;

/// <summary>
/// We inject this class into 'AddTraderWithDynamicAssorts' to help us add items to the trader to sell
/// </summary>
[Injectable(InjectionType.Scoped, TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class FluentTraderAssortCreator(
    DatabaseService databaseService,
    ISptLogger<FluentTraderAssortCreator> logger)
{
    private readonly Dictionary<string, object> assort = new();
    private readonly List<Dictionary<string, object>> itemsToSell = new();
    private readonly Dictionary<string, List<List<object>>> barterScheme = new();
    private readonly Dictionary<string, int> loyaltyLevel = new();

    public FluentTraderAssortCreator CreateSingleAssortItem(TemplateItem tplItem, MongoId? itemId = null, Dictionary<string, object>? upd=null, double? price=1)
    {
        // Create item ready for insertion into assort table
        var tempId = itemId ?? new MongoId();
        Dictionary<string, object> tempUpd = new();
        Dictionary<string, object> tempItem = new();
        Dictionary<string, object> tempBarterScheme = new();

        if (upd != null)
        {
            tempUpd = upd;
        }
        else
        {
            tempUpd.TryAdd("UnlimitedCount", true);
            tempUpd.TryAdd("StackObjectsCount", 999999);
            tempUpd.TryAdd("BuyRestrictionCurrent", 0);
        }
        
        tempItem.TryAdd("_id", tempId.ToString());
        tempItem.TryAdd("_tpl", tplItem.Id.ToString());
        tempItem.TryAdd("parentId", "hideout");
        tempItem.TryAdd("slotId", "hideout");
        tempItem.TryAdd("upd", tempUpd);
        itemsToSell.Add(tempItem);

        tempBarterScheme.TryAdd("count", price);
        tempBarterScheme.TryAdd("_tpl", Money.ROUBLES.ToString());
        barterScheme.TryAdd(
            tempId.ToString(),
            [[tempBarterScheme]]
        );

        loyaltyLevel.TryAdd(tempId.ToString(), 1);

        return this;
    }

    public FluentTraderAssortCreator AddSlotItem(MongoId parantId, MongoId? templateId, string slotId, double? stackCount = null)
    {
        Dictionary<string, object> tempItem = new();
        Dictionary<string, object> tempUpd = new();

        if (stackCount != null)
        {
            tempUpd.TryAdd("StackObjectsCount", stackCount);
        }
        tempItem.TryAdd("_id", new MongoId().ToString());
        tempItem.TryAdd("_tpl", templateId.ToString());
        tempItem.TryAdd("parentId", parantId.ToString());
        tempItem.TryAdd("slotId", slotId.ToString());
        tempItem.TryAdd("upd", tempUpd);

        //logger.Info(JsonSerializer.Serialize(tempItem));
        itemsToSell.Add(tempItem);
        return this;
    }

    public FluentTraderAssortCreator? Clear()
    {
        itemsToSell.Clear();
        barterScheme.Clear();
        loyaltyLevel.Clear();

        return this;
    }

    public FluentTraderAssortCreator? Dump(string path)
    {
        assort.TryAdd("items", itemsToSell);
        assort.TryAdd("barter_scheme", barterScheme);
        assort.TryAdd("loyal_level_items", loyaltyLevel);
        assort.TryAdd("nextResupply", 3600);

        string json = JsonSerializer.Serialize(assort);
        File.WriteAllText(path, json);
        return this;
    }
}
