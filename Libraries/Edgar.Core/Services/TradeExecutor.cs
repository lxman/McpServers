using Alpaca.Markets;
using Edgar.Core.Models;
using Mcp.Common.Core;
using Mcp.Common.Core.Environment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Edgar.Core.Services;

public class TradeExecutor(
    IConfiguration configuration,
    ILogger<TradeExecutor> logger)
{
    private IAlpacaTradingClient? _tradingClient;

    public async Task<MarketStatus> GetMarketStatusAsync(CancellationToken ct = default)
    {
        IAlpacaTradingClient client = GetTradingClient();
        IClock clock = await client.GetClockAsync(ct);
        return new MarketStatus
        {
            IsOpen = clock.IsOpen,
            NextOpen = clock.NextOpenUtc,
            NextClose = clock.NextCloseUtc
        };
    }

    public async Task<decimal> GetAccountValueAsync(CancellationToken ct = default)
    {
        IAlpacaTradingClient client = GetTradingClient();
        IAccount account = await client.GetAccountAsync(ct);
        return account.Equity ?? 0;
    }

    public async Task<Dictionary<string, decimal>> GetExistingPositionsAsync(CancellationToken ct = default)
    {
        IAlpacaTradingClient client = GetTradingClient();
        IReadOnlyList<IPosition> positions = await client.ListPositionsAsync(ct);
        return positions.ToDictionary(
            p => p.Symbol,
            p => p.Quantity);
    }

    public async Task<List<TradeResult>> ExecuteTradesAsync(
        List<TradeRecommendation> recommendations, CancellationToken ct = default)
    {
        IAlpacaTradingClient client = GetTradingClient();
        var results = new List<TradeResult>();

        foreach (TradeRecommendation rec in recommendations)
        {
            var result = new TradeResult
            {
                Ticker = rec.Ticker,
                Action = rec.Action,
                RequestedQuantity = rec.Quantity
            };

            try
            {
                // Use notional (dollar amount) orders for fractional share support
                OrderSide orderSide = rec.Action == TradeAction.Buy ? OrderSide.Buy : OrderSide.Sell;

                var orderRequest = new NewOrderRequest(
                    rec.Ticker,
                    OrderQuantity.Notional(rec.Quantity),
                    orderSide,
                    OrderType.Market,
                    TimeInForce.Day);

                IOrder order = await client.PostOrderAsync(orderRequest, ct);

                result.Success = true;
                result.OrderId = order.OrderId.ToString();

                logger.LogInformation(
                    "Placed {Action} order for {Ticker}: ${Amount:F2} (Order ID: {OrderId})",
                    rec.Action, rec.Ticker, rec.Quantity, result.OrderId);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                logger.LogError(ex, "Failed to execute {Action} for {Ticker}", rec.Action, rec.Ticker);
            }

            results.Add(result);
        }

        return results;
    }

    private IAlpacaTradingClient GetTradingClient()
    {
        if (_tradingClient is not null) return _tradingClient;

        string apiKey = EnvironmentReader.GetEnvironmentVariableWithFallback("APCA_API_KEY_ID")
                        ?? configuration["Alpaca:ApiKey"]
                        ?? throw new InvalidOperationException(
                            "Alpaca API key not found. Set APCA_API_KEY_ID environment variable or configure Alpaca:ApiKey.");

        string secretKey = EnvironmentReader.GetEnvironmentVariableWithFallback("APCA_API_SECRET_KEY")
                           ?? configuration["Alpaca:SecretKey"]
                           ?? throw new InvalidOperationException(
                               "Alpaca secret key not found. Set APCA_API_SECRET_KEY environment variable or configure Alpaca:SecretKey.");

        bool isPaper = configuration.GetValue("Alpaca:PaperTrading", true);

        IEnvironment environment = isPaper
            ? Environments.Paper
            : Environments.Live;

        _tradingClient = environment
            .GetAlpacaTradingClient(new SecretKey(apiKey, secretKey));

        logger.LogInformation("Connected to Alpaca {Environment} trading",
            isPaper ? "paper" : "live");

        return _tradingClient;
    }
}
