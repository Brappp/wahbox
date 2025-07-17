using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace WahBox.External;

public class UniversalisWebSocketClient : IDisposable
{
    private readonly IPluginLog _log;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly string _worldName;
    private readonly Dictionary<uint, Action<MarketDataUpdate>> _itemSubscriptions = new();
    private Task? _receiveTask;
    
    private const string WebSocketUrl = "wss://universalis.app/api/ws";
    
    public UniversalisWebSocketClient(IPluginLog log, string worldName)
    {
        _log = log;
        _worldName = worldName;
    }
    
    public async Task ConnectAsync()
    {
        try
        {
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            await _webSocket.ConnectAsync(new Uri(WebSocketUrl), _cancellationTokenSource.Token);
            _log.Information($"Connected to Universalis WebSocket for world {_worldName}");
            
            // Start receiving messages
            _receiveTask = Task.Run(ReceiveLoop, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to connect to Universalis WebSocket");
            throw;
        }
    }
    
    public async Task SubscribeToItem(uint itemId, uint worldId, Action<MarketDataUpdate> onUpdate)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            await ConnectAsync();
        }
        
        _itemSubscriptions[itemId] = onUpdate;
        
        var subscribeMessage = new
        {
            @event = "subscribe",
            channel = "listings/add",
            worlds = new[] { worldId }
        };
        
        var json = JsonSerializer.Serialize(subscribeMessage);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
        
        _log.Debug($"Subscribed to item {itemId} on world {worldId}");
    }
    
    public async Task UnsubscribeFromItem(uint itemId, uint worldId)
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;
            
        _itemSubscriptions.Remove(itemId);
        
        var unsubscribeMessage = new
        {
            @event = "unsubscribe",
            channel = "listings/add",
            worlds = new[] { worldId }
        };
        
        var json = JsonSerializer.Serialize(unsubscribeMessage);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
        
        _log.Debug($"Unsubscribed from item {itemId} on world {worldId}");
    }
    
    private async Task ReceiveLoop()
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);
        
        while (_webSocket?.State == WebSocketState.Open && !_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                    ProcessMessage(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    _log.Information("WebSocket closed by server");
                    break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in WebSocket receive loop");
                break;
            }
        }
    }
    
    private void ProcessMessage(string message)
    {
        try
        {
            var data = JsonSerializer.Deserialize<UniversalisWebSocketMessage>(message);
            if (data == null) return;
            
            if (data.@event == "listings/add" && data.item.HasValue)
            {
                if (_itemSubscriptions.TryGetValue(data.item.Value, out var callback))
                {
                    var update = new MarketDataUpdate
                    {
                        ItemId = data.item.Value,
                        WorldId = data.world,
                        Listings = data.listings?.Select(l => new MarketListing
                        {
                            PricePerUnit = l.pricePerUnit,
                            Quantity = l.quantity,
                            Total = l.total,
                            IsHQ = l.hq,
                            RetainerName = l.retainerName,
                            LastReviewTime = DateTimeOffset.FromUnixTimeSeconds(l.lastReviewTime).DateTime
                        }).ToList() ?? new List<MarketListing>()
                    };
                    
                    callback(update);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Failed to process WebSocket message: {message}");
        }
    }
    
    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _receiveTask?.Wait(TimeSpan.FromSeconds(5));
        _webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

public class MarketDataUpdate
{
    public uint ItemId { get; set; }
    public uint WorldId { get; set; }
    public List<MarketListing> Listings { get; set; } = new();
    public List<MarketSale> Sales { get; set; } = new();
}

public class MarketListing
{
    public long PricePerUnit { get; set; }
    public int Quantity { get; set; }
    public long Total { get; set; }
    public bool IsHQ { get; set; }
    public string RetainerName { get; set; } = "";
    public DateTime LastReviewTime { get; set; }
}

public class MarketSale
{
    public long PricePerUnit { get; set; }
    public int Quantity { get; set; }
    public long Total { get; set; }
    public bool IsHQ { get; set; }
    public string BuyerName { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

// DTOs for JSON deserialization
internal class UniversalisWebSocketMessage
{
    public string @event { get; set; } = "";
    public uint? item { get; set; }
    public uint world { get; set; }
    public List<WebSocketListing>? listings { get; set; }
    public List<WebSocketSale>? sales { get; set; }
}

internal class WebSocketListing
{
    public bool hq { get; set; }
    public long pricePerUnit { get; set; }
    public int quantity { get; set; }
    public long total { get; set; }
    public string retainerName { get; set; } = "";
    public long lastReviewTime { get; set; }
}

internal class WebSocketSale
{
    public string buyerName { get; set; } = "";
    public bool hq { get; set; }
    public long pricePerUnit { get; set; }
    public int quantity { get; set; }
    public long timestamp { get; set; }
    public long total { get; set; }
}
