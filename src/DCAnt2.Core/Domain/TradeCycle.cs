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
    public TradeDirection Direction { get; }
    public TradeCycleStatus Status { get; private set; }
    public Quantity PositionQuantity { get; private set; }
    public Price PositionVwap { get; private set; }
    
    private readonly Dictionary<InternalOrderId, OrderPurpose> _activeOrders = new();
    
    public int RegisteredOrderCount => _activeOrders.Count;

    public TradeCycle(TradeCycleId id, TradeDirection direction)
    {
        Id = id;
        Direction = direction;
        Status = TradeCycleStatus.Active;
        PositionQuantity = Quantity.Zero;
        PositionVwap = Price.Zero;
    }
    
    public void RegisterOrder(InternalOrderId id, OrderPurpose purpose)
    {
        if (!_activeOrders.TryAdd(id, purpose))
        {
            throw new InvalidOperationException($"Order {id} is already registered.");
        }
    }

    public void UpdatePositionSnapshot(Quantity quantity, Price vwap)
    {
        if (quantity.Value == 0m && vwap.Value != 0m)
        {
            throw new ArgumentException("VWAP must be zero when position is empty.");
        }

        if (quantity.Value > 0m && vwap.Value <= 0m)
        {
            throw new ArgumentException("VWAP must be positive for an open position.");
        }

        PositionQuantity = quantity;
        PositionVwap = vwap;
    }

    public void PauseDca()
    {
        if (Status == TradeCycleStatus.Active)
        {
            Status = TradeCycleStatus.DcaPaused;
        }
    }

    public void EnterExitOnly()
    {
        if (Status == TradeCycleStatus.Completed)
        {
            return;
        }

        Status = TradeCycleStatus.ExitOnly;
    }

    public bool OwnsOrder(InternalOrderId id)
    {
        return _activeOrders.ContainsKey(id);
    }

    public bool TryGetOrderPurpose(InternalOrderId id, out OrderPurpose purpose)
    {
        return _activeOrders.TryGetValue(id, out purpose);
    }
}
