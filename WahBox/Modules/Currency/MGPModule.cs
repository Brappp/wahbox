using WahBox.Core;

namespace WahBox.Modules.Currency;

public class MGPModule : BaseCurrencyModule
{
    public override string Name => "MGP";

    public MGPModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(29); // Manderville Gold Saucer Points
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Manderville Gold Saucer Points";
    }
} 