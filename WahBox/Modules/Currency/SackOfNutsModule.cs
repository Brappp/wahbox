using WahBox.Core;

namespace WahBox.Modules.Currency;

public class SackOfNutsModule : BaseCurrencyModule
{
    public override string Name => "Sacks of Nuts";

    public SackOfNutsModule(Plugin plugin) : base(plugin)
    {
        IconId = 65067; // Sacks of Nuts icon
        _currencyIds.Add(26533); // Sack of Nuts
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Sacks of Nuts";
    }
}
