using WahBox.Core;

namespace WahBox.Modules.Currency;

public class WhiteScripsModule : BaseCurrencyModule
{
    public override string Name => "White Scrips";

    public WhiteScripsModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(25199); // White Scrips
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "White Crafters' Scrips";
    }
}
