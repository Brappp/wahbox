using WahBox.Core;

namespace WahBox.Modules.Currency;

public class TrophyCrystalsModule : BaseCurrencyModule
{
    public override string Name => "Trophy Crystals";

    public TrophyCrystalsModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(36656); // Trophy Crystals
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Trophy Crystals";
    }
}
