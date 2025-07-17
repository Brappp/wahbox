using System.Collections.Generic;
using WahBox.Models;

namespace WahBox.Core.Interfaces;

public interface ICurrencyModule : IModule
{
    List<TrackedCurrency> GetTrackedCurrencies();
    int GetCurrentAmount();
    int GetMaxAmount();
    int AlertThreshold { get; set; }
} 