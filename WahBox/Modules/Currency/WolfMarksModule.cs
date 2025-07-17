using WahBox.Core;

namespace WahBox.Modules.Currency;

public class WolfMarksModule : BaseCurrencyModule
{
    public override string Name => "Wolf Marks";

    public WolfMarksModule(Plugin plugin) : base(plugin)
    {
        _currencyIds.Add(25); // Wolf Marks
    }

    protected override string GetCurrencyName(uint itemId)
    {
        return "Wolf Marks";
    }
}
