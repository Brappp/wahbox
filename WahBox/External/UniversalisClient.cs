using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace WahBox.External;

public class UniversalisClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IPluginLog _log;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly string _worldName;
    
    private const string BaseUrl = "https://universalis.app/api/v2";
    private const int MaxConcurrentRequests = 10; // Increased from 3 to 10
    
    public UniversalisClient(IPluginLog log, string worldName)
    {
        _log = log;
        _worldName = worldName;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WahBox/1.0");
        _rateLimiter = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
    }
    
    public async Task<MarketPriceResult?> GetMarketPrice(uint itemId, bool hq = false, CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var url = $"{BaseUrl}/{_worldName}/{itemId}?entries=1&hq={hq}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning($"Failed to fetch price for item {itemId}: {response.StatusCode}");
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<UniversalisResponse>(json);
            
            if (data?.listings?.Any() == true)
            {
                var listing = data.listings.First();
                return new MarketPriceResult
                {
                    ItemId = itemId,
                    Price = listing.pricePerUnit,
                    IsHQ = listing.hq,
                    WorldName = listing.worldName ?? _worldName,
                    LastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(data.lastUploadTime).DateTime
                };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error fetching price for item {itemId}");
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
    
    public async Task<Dictionary<uint, MarketPriceResult?>> GetMarketPrices(IEnumerable<uint> itemIds, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<uint, MarketPriceResult?>();
        var itemList = itemIds.Distinct().ToList();
        
        // Batch requests to avoid overwhelming the API
        const int batchSize = 100;
        for (int i = 0; i < itemList.Count; i += batchSize)
        {
            var batch = itemList.Skip(i).Take(batchSize).ToList();
            var itemIdString = string.Join(",", batch);
            
            await _rateLimiter.WaitAsync(cancellationToken);
            try
            {
                var url = $"{BaseUrl}/{_worldName}/{itemIdString}?entries=1";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _log.Warning($"Failed to fetch prices for batch: {response.StatusCode}");
                    foreach (var id in batch)
                    {
                        results[id] = null;
                    }
                    continue;
                }
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<UniversalisMultiResponse>(json);
                
                if (data?.items != null)
                {
                    foreach (var kvp in data.items)
                    {
                        if (kvp.Value?.listings?.Any() == true)
                        {
                            var listing = kvp.Value.listings.First();
                            results[kvp.Key] = new MarketPriceResult
                            {
                                ItemId = kvp.Key,
                                Price = listing.pricePerUnit,
                                IsHQ = listing.hq,
                                WorldName = listing.worldName ?? _worldName,
                                LastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(kvp.Value.lastUploadTime).DateTime
                            };
                        }
                        else
                        {
                            results[kvp.Key] = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error fetching prices for batch");
                foreach (var id in batch)
                {
                    results[id] = null;
                }
            }
            finally
            {
                _rateLimiter.Release();
            }
            
            // Small delay between batches
            if (i + batchSize < itemList.Count)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
        
        return results;
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
        _rateLimiter?.Dispose();
    }
}

public class MarketPriceResult
{
    public uint ItemId { get; set; }
    public long Price { get; set; }
    public bool IsHQ { get; set; }
    public string WorldName { get; set; } = "";
    public DateTime LastUpdated { get; set; }
}

// Simplified DTOs for JSON deserialization
internal class UniversalisResponse
{
    public long lastUploadTime { get; set; }
    public List<UniversalisListing>? listings { get; set; }
}

internal class UniversalisMultiResponse
{
    public Dictionary<uint, UniversalisResponse>? items { get; set; }
}

internal class UniversalisListing
{
    public long pricePerUnit { get; set; }
    public int quantity { get; set; }
    public bool hq { get; set; }
    public string? worldName { get; set; }
}
