using WahBox.Core;

namespace WahBox.Modules.Currency;

public class BicolorGemstonesModule : BaseCurrencyModule
{
    public override string Name => "Bicolor Gemstones";

    public BicolorGemstonesModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(26807); // Bicolor Gemstones
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Bicolor Gemstones";
    }
}
