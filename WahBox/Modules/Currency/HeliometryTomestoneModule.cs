using WahBox.Core;

namespace WahBox.Modules.Currency;

public class HeliometryTomestoneModule : BaseCurrencyModule
{
    public override string Name => "Heliometry";

    public HeliometryTomestoneModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(48); // Allagan Tomestone of Heliometry
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Allagan Tomestone of Heliometry";
    }
}
