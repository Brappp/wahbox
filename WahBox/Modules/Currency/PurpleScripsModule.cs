using WahBox.Core;

namespace WahBox.Modules.Currency;

public class PurpleScripsModule : BaseCurrencyModule
{
    public override string Name => "Purple Scrips";

    public PurpleScripsModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(33913); // Purple Scrips
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Purple Crafters' Scrips";
    }
}
