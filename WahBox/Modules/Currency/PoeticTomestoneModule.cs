using WahBox.Core;

namespace WahBox.Modules.Currency;

public class PoeticTomestoneModule : BaseCurrencyModule
{
    public override string Name => "Poetics";

    public PoeticTomestoneModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(28); // Allagan Tomestone of Poetics
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Allagan Tomestone of Poetics";
    }
}
