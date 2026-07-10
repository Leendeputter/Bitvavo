using System.Collections.Concurrent;
using BitvavoBot.Domain.Enums;
using BitvavoBot.Domain.Interfaces;
using BitvavoBot.Exchange.Bitvavo;
using BitvavoBot.Exchange.Paper;

namespace BitvavoBot.App.Composition;

/// <summary>
/// Resolves the correct <see cref="IExchangeClient"/> per bot profile: the single Live Bitvavo
/// client for Mode = Live, or a <see cref="PaperExchangeClient"/> bound to the requested
/// Papertrading profile for Mode = Paper (one instance per profile, cached and reused so all
/// callers share the same simulated order book). Both read live market data from the same
/// <see cref="BitvavoExchangeClient"/> instance (functional spec 8.2).
/// </summary>
public sealed class ExchangeClientFactory : IExchangeClientFactory
{
    private readonly BitvavoExchangeClient _liveClient;
    private readonly IOrderRepository _orderRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IPapertradingProfileRepository _papertradingProfileRepository;
    private readonly ConcurrentDictionary<string, PaperExchangeClient> _paperClients = new();
    private readonly ConcurrentDictionary<string, PaperLeverageClient> _paperLeverageClients = new();

    public ExchangeClientFactory(
        BitvavoExchangeClient liveClient, IOrderRepository orderRepository, ITradeRepository tradeRepository,
        IPositionRepository positionRepository, IPapertradingProfileRepository papertradingProfileRepository)
    {
        _liveClient = liveClient;
        _orderRepository = orderRepository;
        _tradeRepository = tradeRepository;
        _positionRepository = positionRepository;
        _papertradingProfileRepository = papertradingProfileRepository;
    }

    public IExchangeClient GetClient(TradingMode mode, string? papertradingProfileName)
    {
        if (mode == TradingMode.Live) return _liveClient;

        if (string.IsNullOrWhiteSpace(papertradingProfileName))
        {
            throw new InvalidOperationException("A Papertrading profile name is required for Mode = Paper.");
        }

        return _paperClients.GetOrAdd(papertradingProfileName, name => new PaperExchangeClient(
            _liveClient, _orderRepository, _tradeRepository, _positionRepository, _papertradingProfileRepository, name));
    }

    /// <summary>
    /// Returns the single <see cref="PaperLeverageClient"/> instance for this profile, cached for
    /// the app's lifetime — the client tracks open positions in memory (keyed by a generated
    /// external id), so a fresh instance per call would make positions vanish immediately after
    /// opening them.
    /// </summary>
    public PaperLeverageClient GetPaperLeverageClient(string papertradingProfileName) =>
        _paperLeverageClients.GetOrAdd(papertradingProfileName, name => new PaperLeverageClient(_liveClient, _positionRepository, name));
}
