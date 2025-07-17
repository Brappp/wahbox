using WahBox.Core;

namespace WahBox.Modules.Currency;

public class GrandCompanyModule : BaseCurrencyModule
{
    public override string Name => "Grand Company Seals";

    public GrandCompanyModule(Plugin plugin) : base(plugin)
    {
        // Add all three GC seal types
        _currencyIds.Add(20); // Storm Seals
        _currencyIds.Add(21); // Serpent Seals
        _currencyIds.Add(22); // Flame Seals
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return itemId switch
        {
            20 => "Storm Seals",
            21 => "Serpent Seals",
            22 => "Flame Seals",
            _ => "Grand Company Seals"
        };
    }
}
