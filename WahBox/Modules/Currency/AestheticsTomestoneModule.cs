using WahBox.Core;

namespace WahBox.Modules.Currency;

public class AestheticsTomestoneModule : BaseCurrencyModule
{
    public override string Name => "Aesthetics";

    public AestheticsTomestoneModule(Plugin plugin) : base(plugin)
    {
        IconId = 65076; // Aesthetics icon
        _currencyIds.Add(47); // Allagan Tomestone of Aesthetics
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Allagan Tomestone of Aesthetics";
    }
}
