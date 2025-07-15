using System.Collections.Generic;
using SamplePlugin.Models;

namespace SamplePlugin.Core.Interfaces;

public interface ICurrencyModule : IModule
{
    List<TrackedCurrency> GetTrackedCurrencies();
} 