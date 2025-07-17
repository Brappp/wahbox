using WahBox.Core;

namespace WahBox.Modules.Currency;

public class AlliedSealsModule : BaseCurrencyModule
{
    public override string Name => "Allied Seals";

    public AlliedSealsModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(27); // Allied Seals
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Allied Seals";
    }
}
