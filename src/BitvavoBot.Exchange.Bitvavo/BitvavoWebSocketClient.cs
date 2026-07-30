using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BitvavoBot.Domain.Enums;

namespace BitvavoBot.Exchange.Bitvavo;

/// <summary>
/// Thin wrapper around Bitvavo's public WebSocket feed. Automatically reconnects with
/// exponential backoff (functional spec 11) and re-subscribes to whatever markets were
/// subscribed to before the drop.
/// </summary>
public sealed class BitvavoWebSocketClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
    };

    private readonly BitvavoOptions _options;
    private readonly HashSet<string> _tickerSubscriptions = new();
    private readonly HashSet<string> _bookSubscriptions = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _runLoopCts;
    private Task? _runLoopTask;

    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
    public event EventHandler<JsonElement>? MessageReceived;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    public BitvavoWebSocketClient(BitvavoOptions options)
    {
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_runLoopTask is not null) return Task.CompletedTask;
        _runLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runLoopTask = Task.Run(() => RunLoopAsync(_runLoopCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_runLoopCts is null) return;
        _runLoopCts.Cancel();
        try { if (_runLoopTask is not null) await _runLoopTask; } catch (OperationCanceledException) { }
        _runLoopTask = null;
        SetStatus(ConnectionStatus.Disconnected);
    }

    public async Task SubscribeTickerAsync(string market, CancellationToken cancellationToken = default)
    {
        _tickerSubscriptions.Add(market);
        if (Status == ConnectionStatus.Connected)
        {
            await SendSubscriptionAsync("ticker24h", new[] { market }, cancellationToken);
        }
    }

    public async Task UnsubscribeTickerAsync(string market, CancellationToken cancellationToken = default)
    {
        _tickerSubscriptions.Remove(market);
        if (Status == ConnectionStatus.Connected)
        {
            await SendUnsubscriptionAsync("ticker24h", new[] { market }, cancellationToken);
        }
    }

    public async Task SubscribeOrderBookAsync(string market, CancellationToken cancellationToken = default)
    {
        _bookSubscriptions.Add(market);
        if (Status == ConnectionStatus.Connected)
        {
            await SendSubscriptionAsync("book", new[] { market }, cancellationToken);
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SetStatus(attempt == 0 ? ConnectionStatus.Connecting : ConnectionStatus.Reconnecting);
                await ConnectAndListenAsync(cancellationToken);
                attempt = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                SetStatus(ConnectionStatus.FallbackPolling);
                var delay = BackoffSchedule[Math.Min(attempt, BackoffSchedule.Length - 1)];
                attempt++;
                try { await Task.Delay(delay, cancellationToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ConnectAndListenAsync(CancellationToken cancellationToken)
    {
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(_options.WebSocketUrl), cancellationToken);
        SetStatus(ConnectionStatus.Connected);

        foreach (var market in _tickerSubscriptions)
        {
            await SendSubscriptionAsync("ticker24h", new[] { market }, cancellationToken);
        }
        foreach (var market in _bookSubscriptions)
        {
            await SendSubscriptionAsync("book", new[] { market }, cancellationToken);
        }

        var buffer = new byte[16 * 1024];
        while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new IOException("Bitvavo WebSocket closed by remote host.");
                }
                messageStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            messageStream.Position = 0;
            try
            {
                using var document = await JsonDocument.ParseAsync(messageStream, cancellationToken: cancellationToken);
                // A subscriber throwing here must never tear down this receive loop/reconnect the
                // whole socket over one bad message — callers are expected to guard their own
                // parsing, but this is cheap defense-in-depth against a future regression there.
                MessageReceived?.Invoke(this, document.RootElement.Clone());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
            }
        }
    }

    private Task SendSubscriptionAsync(string channel, IReadOnlyList<string> markets, CancellationToken cancellationToken) =>
        SendAsync(new { action = "subscribe", channels = new[] { new { name = channel, markets } } }, cancellationToken);

    private Task SendUnsubscriptionAsync(string channel, IReadOnlyList<string> markets, CancellationToken cancellationToken) =>
        SendAsync(new { action = "unsubscribe", channels = new[] { new { name = channel, markets } } }, cancellationToken);

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        if (_socket is not { State: WebSocketState.Open }) return;
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private void SetStatus(ConnectionStatus status)
    {
        if (Status == status) return;
        Status = status;
        ConnectionStatusChanged?.Invoke(this, status);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _socket?.Dispose();
        _runLoopCts?.Dispose();
    }
}
