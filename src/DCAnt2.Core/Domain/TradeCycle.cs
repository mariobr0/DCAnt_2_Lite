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

public enum TradeCycleExitReason
{
    None,
    TakeProfit,
    StopLoss
}

public class TradeCycle
{
    public TradeCycleId Id { get; }
    public TradeCycleStatus Status { get; private set; }
    public TradeCycleExitReason ExitReason { get; private set; }
    public Quantity PositionQuantity { get; private set; }
    public Price PositionVwap { get; private set; }
    
    // Tracks active orders internally to map execution events to purposes
    private readonly Dictionary<InternalOrderId, OrderPurpose> _activeOrders = new();
    
    // Track current TP/SL so we can cancel it
    private InternalOrderId? _currentTpId;
    private InternalOrderId? _currentSlId;
    
    private readonly DcaSettings _settings;
    private readonly InstrumentRules _rules;
    private readonly OrderSide _cycleSide;

    private int _generatedGridLevels = 0;
    private int _filledGridLevels = 0;
    private int _activeGridOrdersCount = 0;

    private Price _entryPrice;
    private Quantity _baseQuantity;
    private Price? _lastTpPrice;
    private Price? _slPrice;
    private bool _exitOrdersUpdateRequired;

    private readonly List<TradeIntent> _outbox = new();
    public IReadOnlyList<TradeIntent> Outbox => _outbox;
    
    public void ClearOutbox() => _outbox.Clear();

    public TradeCycle(TradeCycleId id, DcaSettings settings, InstrumentRules rules, OrderSide cycleSide)
    {
        if (settings.TpPercent <= 0) throw new ArgumentException("Take profit percentage must be positive.");
        
        Id = id;
        Status = TradeCycleStatus.Active;
        ExitReason = TradeCycleExitReason.None;
        PositionQuantity = Quantity.Zero;
        PositionVwap = Price.Zero;
        _settings = settings;
        _rules = rules;
        _cycleSide = cycleSide;
    }
    
    public void Start(Price price, Quantity quantity)
    {
        if (Status != TradeCycleStatus.Active) return;
        
        _entryPrice = price;
        _baseQuantity = quantity;

        var id = InternalOrderId.New();
        _activeOrders[id] = OrderPurpose.FirstOrder;
        _activeGridOrdersCount++;
        
        _outbox.Add(new PlaceOrderIntent(id, OrderPurpose.FirstOrder, _cycleSide, price, quantity));
        
        if (_settings.LastLevelStopPercent > 0)
        {
            decimal totalDropRaw = _settings.StepPercent / 100m * _settings.MaxGridLevels;
            decimal lastGridPriceRaw = _cycleSide == OrderSide.Buy 
                ? _entryPrice.Value * (1 - totalDropRaw)
                : _entryPrice.Value * (1 + totalDropRaw);

            decimal slPriceRaw = _cycleSide == OrderSide.Buy
                ? lastGridPriceRaw * (1 - _settings.LastLevelStopPercent / 100m)
                : lastGridPriceRaw * (1 + _settings.LastLevelStopPercent / 100m);

            _slPrice = _rules.RoundPrice(new Price(slPriceRaw));
        }

        RefillGridWindow();
    }

    private void RefillGridWindow()
    {
        if (Status != TradeCycleStatus.Active) return;

        while (_activeGridOrdersCount < _settings.ActiveGridWindow && _generatedGridLevels < _settings.MaxGridLevels)
        {
            _generatedGridLevels++;
            
            decimal stepRaw = _settings.StepPercent / 100m * _generatedGridLevels;
            decimal priceRaw = _cycleSide == OrderSide.Buy 
                ? _entryPrice.Value * (1 - stepRaw)
                : _entryPrice.Value * (1 + stepRaw);

            decimal qtyRaw = _baseQuantity.Value * (decimal)Math.Pow((double)_settings.VolumeScale, _generatedGridLevels);

            Price gridPrice = _rules.RoundPrice(new Price(priceRaw));
            Quantity gridQty = _rules.RoundQuantityDown(new Quantity(qtyRaw));

            var id = InternalOrderId.New();
            _activeOrders[id] = OrderPurpose.DcaOrder;
            _activeGridOrdersCount++;
            
            _outbox.Add(new PlaceOrderIntent(id, OrderPurpose.DcaOrder, _cycleSide, gridPrice, gridQty));
        }
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
            ExitReason = purpose == OrderPurpose.TakeProfit ? TradeCycleExitReason.TakeProfit : TradeCycleExitReason.StopLoss;
            
            // Cancel remaining active orders (like unfilled DCA grid)
            foreach (var kvp in _activeOrders)
            {
                _outbox.Add(new CancelOrderIntent(kvp.Key));
            }
            _activeOrders.Clear();
            return;
        }

        if (purpose == OrderPurpose.FirstOrder || purpose == OrderPurpose.DcaOrder)
        {
            _activeGridOrdersCount--;
            if (purpose == OrderPurpose.DcaOrder)
                _filledGridLevels++;
        }

        // It's a FirstOrder or DcaOrder -> Update Position and VWAP
        decimal currentTotalValue = PositionQuantity.Value * PositionVwap.Value;
        decimal executionValue = evt.ExecutedQuantity.Value * evt.ExecutedPrice.Value;
        
        decimal newQuantityValue = PositionQuantity.Value + evt.ExecutedQuantity.Value;
        
        PositionQuantity = new Quantity(newQuantityValue);
        PositionVwap = new Price((currentTotalValue + executionValue) / newQuantityValue);

        _exitOrdersUpdateRequired = true;
        RefillGridWindow();
    }

    public void Handle(TickMessage evt)
    {
        if (Status != TradeCycleStatus.Active || !_exitOrdersUpdateRequired || PositionQuantity.Value == 0)
            return;

        ReplaceExitOrders();
        _exitOrdersUpdateRequired = false;
    }

    public void Handle(OrderRejected evt)
    {
        if (!_activeOrders.TryGetValue(evt.OrderId, out var purpose))
            return;
            
        _activeOrders.Remove(evt.OrderId);

        // If a DCA or First order fails, we cannot continue grid, must enter ExitOnly
        if (purpose == OrderPurpose.DcaOrder || purpose == OrderPurpose.FirstOrder)
        {
            _activeGridOrdersCount--;
            Status = TradeCycleStatus.ExitOnly;
        }
    }

    private void ReplaceExitOrders()
    {
        // 1. Take Profit
        decimal tpPriceRaw = _cycleSide == OrderSide.Buy 
            ? PositionVwap.Value * (1 + _settings.TpPercent / 100m)
            : PositionVwap.Value * (1 - _settings.TpPercent / 100m);

        Price tpPrice = _rules.RoundPrice(new Price(tpPriceRaw));
        OrderSide tpSide = _cycleSide == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        
        if (_currentTpId.HasValue)
        {
            _outbox.Add(new CancelOrderIntent(_currentTpId.Value));
            _activeOrders.Remove(_currentTpId.Value);
        }

        _lastTpPrice = tpPrice;
        
        _currentTpId = InternalOrderId.New();
        _activeOrders[_currentTpId.Value] = OrderPurpose.TakeProfit;
        
        _outbox.Add(new PlaceOrderIntent(_currentTpId.Value, OrderPurpose.TakeProfit, tpSide, tpPrice, PositionQuantity));

        // 2. Stop Loss
        if (_slPrice.HasValue)
        {
            if (_currentSlId.HasValue)
            {
                _outbox.Add(new CancelOrderIntent(_currentSlId.Value));
                _activeOrders.Remove(_currentSlId.Value);
            }

            _currentSlId = InternalOrderId.New();
            _activeOrders[_currentSlId.Value] = OrderPurpose.StopLoss;
            
            _outbox.Add(new PlaceOrderIntent(_currentSlId.Value, OrderPurpose.StopLoss, tpSide, _slPrice.Value, PositionQuantity));
        }
    }

    public TradeCycleSnapshot GetSnapshot()
    {
        return new TradeCycleSnapshot(
            _generatedGridLevels,
            _filledGridLevels,
            _activeGridOrdersCount,
            _entryPrice.Value,
            _baseQuantity.Value,
            _lastTpPrice?.Value,
            _exitOrdersUpdateRequired,
            _slPrice?.Value
        );
    }

    public void RestoreSnapshot(TradeCycleSnapshot snapshot)
    {
        _generatedGridLevels = snapshot.GeneratedGridLevels;
        _filledGridLevels = snapshot.FilledGridLevels;
        _activeGridOrdersCount = snapshot.ActiveGridOrdersCount;
        _entryPrice = new Price(snapshot.EntryPrice);
        _baseQuantity = new Quantity(snapshot.BaseQuantity);
        _lastTpPrice = snapshot.LastTpPrice.HasValue ? new Price(snapshot.LastTpPrice.Value) : null;
        _exitOrdersUpdateRequired = snapshot.TpUpdateRequired;
        _slPrice = snapshot.SlPrice.HasValue ? new Price(snapshot.SlPrice.Value) : null;
    }

    public void RestorePosition(Quantity position, Price vwap)
    {
        PositionQuantity = position;
        PositionVwap = vwap;
    }

    public void RestoreOrder(InternalOrderId orderId, OrderPurpose purpose)
    {
        _activeOrders[orderId] = purpose;
        if (purpose == OrderPurpose.TakeProfit)
            _currentTpId = orderId;
        if (purpose == OrderPurpose.StopLoss)
            _currentSlId = orderId;
    }
}
