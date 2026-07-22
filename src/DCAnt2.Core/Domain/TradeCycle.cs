using System;
using System.Collections.Generic;

namespace DCAnt2.Core.Domain;

public enum TradeCycleStatus
{
    Active,
    DcaPaused,
    ExitOnly,
    Completed
}

public class TradeCycle
{
    public TradeCycleId Id { get; }
    public TradeCycleStatus Status { get; private set; }
    public Quantity PositionQuantity { get; private set; }
    public Price PositionVwap { get; private set; }
    
    // Tracks active orders internally to map execution events to purposes
    private readonly Dictionary<InternalOrderId, OrderPurpose> _activeOrders = new();
    
    // Track current TP so we can cancel it
    private InternalOrderId? _currentTpId;
    
    private readonly decimal _tpPercent;
    private readonly InstrumentRules _rules;
    private readonly OrderSide _cycleSide;

    private readonly List<TradeIntent> _outbox = new();
    public IReadOnlyList<TradeIntent> Outbox => _outbox;
    
    public void ClearOutbox() => _outbox.Clear();

    public TradeCycle(TradeCycleId id, decimal tpPercent, InstrumentRules rules, OrderSide cycleSide)
    {
        if (tpPercent <= 0) throw new ArgumentException("Take profit percentage must be positive.");
        
        Id = id;
        Status = TradeCycleStatus.Active;
        PositionQuantity = Quantity.Zero;
        PositionVwap = Price.Zero;
        _tpPercent = tpPercent;
        _rules = rules;
        _cycleSide = cycleSide;
    }
    
    public void Start(Price price, Quantity quantity)
    {
        if (Status != TradeCycleStatus.Active) return;
        
        var id = InternalOrderId.New();
        _activeOrders[id] = OrderPurpose.FirstOrder;
        _outbox.Add(new PlaceOrderIntent(id, OrderPurpose.FirstOrder, _cycleSide, price, quantity));
    }
    
    public void PlaceDca(Price price, Quantity quantity)
    {
        if (Status != TradeCycleStatus.Active) return;
        
        var id = InternalOrderId.New();
        _activeOrders[id] = OrderPurpose.DcaOrder;
        _outbox.Add(new PlaceOrderIntent(id, OrderPurpose.DcaOrder, _cycleSide, price, quantity));
    }

    public void Handle(OrderExecuted evt)
    {
        if (!_activeOrders.TryGetValue(evt.OrderId, out var purpose))
            return; // Order doesn't belong to this cycle or was already handled
            
        _activeOrders.Remove(evt.OrderId);
        
        if (purpose == OrderPurpose.TakeProfit || purpose == OrderPurpose.StopLoss)
        {
            PositionQuantity = Quantity.Zero;
            Status = TradeCycleStatus.Completed;
            return;
        }

        // It's a FirstOrder or DcaOrder -> Update Position and VWAP
        decimal currentTotalValue = PositionQuantity.Value * PositionVwap.Value;
        decimal executionValue = evt.ExecutedQuantity.Value * evt.ExecutedPrice.Value;
        
        decimal newQuantityValue = PositionQuantity.Value + evt.ExecutedQuantity.Value;
        
        PositionQuantity = new Quantity(newQuantityValue);
        PositionVwap = new Price((currentTotalValue + executionValue) / newQuantityValue);

        ReplaceTakeProfit();
    }

    public void Handle(OrderRejected evt)
    {
        if (!_activeOrders.TryGetValue(evt.OrderId, out var purpose))
            return;
            
        _activeOrders.Remove(evt.OrderId);

        // If a DCA or First order fails, we cannot continue grid, must enter ExitOnly
        if (purpose == OrderPurpose.DcaOrder || purpose == OrderPurpose.FirstOrder)
        {
            Status = TradeCycleStatus.ExitOnly;
        }
    }

    private void ReplaceTakeProfit()
    {
        if (_currentTpId.HasValue)
        {
            _outbox.Add(new CancelOrderIntent(_currentTpId.Value));
            _activeOrders.Remove(_currentTpId.Value);
        }

        decimal tpPriceRaw = _cycleSide == OrderSide.Buy 
            ? PositionVwap.Value * (1 + _tpPercent / 100m)
            : PositionVwap.Value * (1 - _tpPercent / 100m);

        Price tpPrice = _rules.RoundPrice(new Price(tpPriceRaw));
        OrderSide tpSide = _cycleSide == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        
        _currentTpId = InternalOrderId.New();
        _activeOrders[_currentTpId.Value] = OrderPurpose.TakeProfit;
        
        _outbox.Add(new PlaceOrderIntent(_currentTpId.Value, OrderPurpose.TakeProfit, tpSide, tpPrice, PositionQuantity));
    }
}
