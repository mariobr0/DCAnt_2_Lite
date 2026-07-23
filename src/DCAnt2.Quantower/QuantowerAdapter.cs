using System;
using System.Collections.Generic;
using System.Linq;
using DCAnt2.Core.Domain;
using TradingPlatform.BusinessLayer;

namespace DCAnt2.Quantower;

public class QuantowerAdapter
{
    private readonly EngineLoop _engineLoop;
    private readonly Account _account;
    private readonly Symbol _symbol;

    public QuantowerAdapter(EngineLoop engineLoop, Account account, Symbol symbol)
    {
        _engineLoop = engineLoop ?? throw new ArgumentNullException(nameof(engineLoop));
        _account = account ?? throw new ArgumentNullException(nameof(account));
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
    }

    /// <summary>
    /// Task 1: Процесс Reconciliation (докачка ордеров и сделок)
    /// Сверяет ожидаемые локально ордера с фактическим состоянием на бирже.
    /// </summary>
    public void Reconcile(IEnumerable<InternalOrderId> expectedOrderIds)
    {
        var expectedSet = expectedOrderIds.Select(id => id.Value).ToHashSet();
        if (!expectedSet.Any()) return; // Нечего сверять

        // Ищем наши активные ордера (чтобы убедиться, что они не пропали)
        var activeOrders = TradingPlatform.BusinessLayer.Core.Instance.Orders
            .Where(o => o.Account == _account && o.Symbol == _symbol && o.Status == OrderStatus.Opened)
            .ToList();

        // Ищем наши ордера в истории за сессию
        var allOrders = TradingPlatform.BusinessLayer.Core.Instance.Orders
            .Where(o => o.Account == _account && o.Symbol == _symbol)
            .ToList();
        
        foreach (var order in allOrders)
        {
            if (!string.IsNullOrEmpty(order.Comment) && expectedSet.Contains(order.Comment))
            {
                // Проверяем, есть ли исполнения
                if (order.Status == OrderStatus.Filled || order.Status == OrderStatus.PartiallyFilled)
                {
                    // Для этапа 8 (одна сделка без DCA) мы можем генерировать ExecutionId на основе ID ордера
                    // В реальном боевом коде лучше парсить trade.Id из order.GetTrades()
                    var executionId = new ExecutionId("exec_" + order.Id);
                    var internalOrderId = new InternalOrderId(order.Comment);
                    var price = new DCAnt2.Core.Domain.Price((decimal)order.Price);
                    var qty = new Quantity((decimal)order.FilledQuantity);
                    
                    var execution = new OrderExecuted(executionId, internalOrderId, price, qty);
                    _engineLoop.Enqueue(new ExecutionMessage(execution));
                }
            }
        }
    }

    /// <summary>
    /// Task 2: Диспетчеризация Outbox -> Quantower API
    /// </summary>
    public void ProcessOutbox(IEnumerable<TradeIntent> intents)
    {
        foreach (var intent in intents)
        {
            if (intent is PlaceOrderIntent placeIntent)
            {
                var parameters = new PlaceOrderRequestParameters
                {
                    Account = _account,
                    Symbol = _symbol,
                    Side = placeIntent.Side == DCAnt2.Core.Domain.OrderSide.Buy ? TradingPlatform.BusinessLayer.Side.Buy : TradingPlatform.BusinessLayer.Side.Sell,
                    TimeInForce = TimeInForce.GTC,
                    OrderTypeId = OrderType.Limit,
                    Price = (double)placeIntent.Price.Value,
                    Quantity = (double)placeIntent.Quantity.Value,
                    Comment = placeIntent.OrderId.Value // ClientRequestId is Comment
                };
                
                // В Quantower API PlaceOrder асинхронный по природе (отправляет запрос),
                // результат возвращается либо синхронно (ошибка валидации), либо через коллбеки
                TradingPlatform.BusinessLayer.Core.Instance.PlaceOrder(parameters);
            }
            else if (intent is CancelOrderIntent cancelIntent)
            {
                // Ищем ордер по нашему ID (Comment)
                var order = TradingPlatform.BusinessLayer.Core.Instance.Orders
                    .FirstOrDefault(o => o.Comment == cancelIntent.OrderId.Value && (o.Status == OrderStatus.Opened || o.Status == OrderStatus.PartiallyFilled));
                    
                if (order != null)
                {
                    TradingPlatform.BusinessLayer.Core.Instance.CancelOrder((IOrder)order);
                }
            }
        }
    }

    /// <summary>
    /// Task 3: Маршрутизация Quantower Callbacks -> EngineLoop
    /// Этот метод должен вызываться из Strategy.OnOrderUpdated
    /// </summary>
    public void HandleOrderUpdated(Order order)
    {
        if (order.Account.Id != _account.Id || order.Symbol.Id != _symbol.Id) return;
        if (string.IsNullOrEmpty(order.Comment)) return; // Не наш ордер (нет ClientRequestId)
        
        var internalOrderId = new InternalOrderId(order.Comment);

        if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refused)
        {
            _engineLoop.Enqueue(new RejectionMessage(new OrderRejected(internalOrderId, "Cancelled or Refused")));
        }
        else if (order.Status == OrderStatus.Filled || order.Status == OrderStatus.PartiallyFilled)
        {
            var executionId = new ExecutionId("exec_" + order.Id);
            var price = new DCAnt2.Core.Domain.Price((decimal)order.Price);
            var qty = new Quantity((decimal)order.FilledQuantity);
            var execution = new OrderExecuted(executionId, internalOrderId, price, qty);
            
            _engineLoop.Enqueue(new ExecutionMessage(execution));
        }
    }

    public void HandleOrderHistoryAdded(OrderHistory order)
    {
        if (order.Account.Id != _account.Id || order.Symbol.Id != _symbol.Id) return;
        if (string.IsNullOrEmpty(order.Comment)) return; 
        
        var internalOrderId = new InternalOrderId(order.Comment);

        if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refused)
        {
            _engineLoop.Enqueue(new RejectionMessage(new OrderRejected(internalOrderId, "Cancelled or Refused")));
        }
        else if (order.Status == OrderStatus.Filled || order.Status == OrderStatus.PartiallyFilled)
        {
            var executionId = new ExecutionId("exec_" + order.Id);
            var price = new DCAnt2.Core.Domain.Price((decimal)order.Price);
            var qty = new Quantity((decimal)order.FilledQuantity);
            var execution = new OrderExecuted(executionId, internalOrderId, price, qty);
            
            _engineLoop.Enqueue(new ExecutionMessage(execution));
        }
    }

    /// <summary>
    /// Task 3: Маршрутизация Quantower Callbacks -> EngineLoop
    /// Этот метод должен вызываться из Strategy.OnTradeAdded или аналогичного (в Quantower это OnExecutionReport / OnTrade)
    /// </summary>
    // Закомментировано до момента точного выяснения свойств Trade
    // public void HandleTradeAdded(Trade trade)
    // {
    //    // ...
    // }
}
