using WahBox.Core;

namespace WahBox.Modules.Currency;

public class SkybuildersScripModule : BaseCurrencyModule
{
    public override string Name => "Skybuilders' Scrips";

    public SkybuildersScripModule(Plugin plugin) : base(plugin)
    {
        IconId = 65073; // Skybuilders' Scrips icon
        _currencyIds.Add(28063); // Skybuilders' Scrips
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Skybuilders' Scrips";
    }
}
