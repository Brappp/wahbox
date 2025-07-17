using WahBox.Core;

namespace WahBox.Modules.Currency;

public class CenturioSealsModule : BaseCurrencyModule
{
    public override string Name => "Centurio Seals";

    public CenturioSealsModule(Plugin plugin) : base(plugin)
    {
        IconId = 65065; // Centurio Seals icon
        _currencyIds.Add(10307); // Centurio Seals
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Centurio Seals";
    }
}
