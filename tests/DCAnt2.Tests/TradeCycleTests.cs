using System;
using DCAnt2.Core.Domain;
using Xunit;

namespace DCAnt2.Tests;

public class TradeCycleTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        var id = TradeCycleId.New();
        var cycle = new TradeCycle(id, TradeDirection.Long);

        Assert.Equal(id, cycle.Id);
        Assert.Equal(TradeDirection.Long, cycle.Direction);
        Assert.Equal(TradeCycleStatus.Active, cycle.Status);
        Assert.Equal(0m, cycle.PositionQuantity.Value);
        Assert.Equal(0m, cycle.PositionVwap.Value);
        Assert.Equal(0, cycle.RegisteredOrderCount);
    }

    [Fact]
    public void RegisterOrder_RegistersOwnership()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Short);
        var orderId = InternalOrderId.New();

        cycle.RegisterOrder(orderId, OrderPurpose.FirstOrder);

        Assert.Equal(1, cycle.RegisteredOrderCount);
        Assert.True(cycle.OwnsOrder(orderId));
    }

    [Fact]
    public void RegisterOrder_StoresPurpose()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Short);
        var orderId = InternalOrderId.New();

        cycle.RegisterOrder(orderId, OrderPurpose.DcaOrder);

        var found = cycle.TryGetOrderPurpose(orderId, out var purpose);

        Assert.True(found);
        Assert.Equal(OrderPurpose.DcaOrder, purpose);
    }

    [Fact]
    public void RegisterOrder_Twice_ThrowsInvalidOperationException()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        var orderId = InternalOrderId.New();

        cycle.RegisterOrder(orderId, OrderPurpose.TakeProfit);

        var ex = Assert.Throws<InvalidOperationException>(() => 
            cycle.RegisterOrder(orderId, OrderPurpose.TakeProfit));
            
        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void OwnsOrder_ForUnknownOrder_ReturnsFalse()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        var orderId = InternalOrderId.New();

        Assert.False(cycle.OwnsOrder(orderId));
    }

    [Fact]
    public void TryGetOrderPurpose_ForUnknownOrder_ReturnsFalse()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        var orderId = InternalOrderId.New();

        Assert.False(cycle.TryGetOrderPurpose(orderId, out _));
    }

    [Fact]
    public void UpdatePositionSnapshot_ValidOpenPosition_UpdatesValues()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);

        cycle.UpdatePositionSnapshot(new Quantity(100m), new Price(0.105m));

        Assert.Equal(100m, cycle.PositionQuantity.Value);
        Assert.Equal(0.105m, cycle.PositionVwap.Value);
    }

    [Fact]
    public void UpdatePositionSnapshot_ValidEmptyPosition_UpdatesValues()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        cycle.UpdatePositionSnapshot(new Quantity(100m), new Price(0.105m));
        
        cycle.UpdatePositionSnapshot(new Quantity(0m), new Price(0m));

        Assert.Equal(0m, cycle.PositionQuantity.Value);
        Assert.Equal(0m, cycle.PositionVwap.Value);
    }

    [Fact]
    public void UpdatePositionSnapshot_ReplacesPreviousValues()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        cycle.UpdatePositionSnapshot(new Quantity(100m), new Price(0.100m));
        
        // This is a replacement, not addition
        cycle.UpdatePositionSnapshot(new Quantity(80m), new Price(0.102m));

        Assert.Equal(80m, cycle.PositionQuantity.Value);
        Assert.Equal(0.102m, cycle.PositionVwap.Value);
    }

    [Fact]
    public void UpdatePositionSnapshot_EmptyQuantityWithPositiveVwap_ThrowsArgumentExceptionAndKeepsOldState()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        cycle.UpdatePositionSnapshot(new Quantity(100m), new Price(0.105m));

        var ex = Assert.Throws<ArgumentException>(() => 
            cycle.UpdatePositionSnapshot(new Quantity(0m), new Price(0.1045m)));
            
        Assert.Contains("VWAP must be zero when position is empty", ex.Message);
        
        // Ensure state was not corrupted
        Assert.Equal(100m, cycle.PositionQuantity.Value);
        Assert.Equal(0.105m, cycle.PositionVwap.Value);
    }

    [Fact]
    public void UpdatePositionSnapshot_PositiveQuantityWithZeroVwap_ThrowsArgumentExceptionAndKeepsOldState()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        cycle.UpdatePositionSnapshot(new Quantity(100m), new Price(0.105m));

        var ex = Assert.Throws<ArgumentException>(() => 
            cycle.UpdatePositionSnapshot(new Quantity(50m), new Price(0m)));
            
        Assert.Contains("VWAP must be positive for an open position", ex.Message);
        
        // Ensure state was not corrupted
        Assert.Equal(100m, cycle.PositionQuantity.Value);
        Assert.Equal(0.105m, cycle.PositionVwap.Value);
    }

    [Fact]
    public void EnterExitOnly_ChangesStatusToExitOnly()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        
        cycle.EnterExitOnly();
        
        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
    }

    [Fact]
    public void EnterExitOnly_IsIdempotent()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        
        cycle.EnterExitOnly();
        cycle.EnterExitOnly();
        
        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
    }

    [Fact]
    public void PauseDca_WhenActive_ChangesStatusToDcaPaused()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        
        cycle.PauseDca();
        
        Assert.Equal(TradeCycleStatus.DcaPaused, cycle.Status);
    }

    [Fact]
    public void PauseDca_IsIdempotent()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        
        cycle.PauseDca();
        cycle.PauseDca();
        
        Assert.Equal(TradeCycleStatus.DcaPaused, cycle.Status);
    }

    [Fact]
    public void PauseDca_WhenExitOnly_DoesNotWeakenStatus()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        
        cycle.EnterExitOnly();
        cycle.PauseDca();
        
        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
    }

    [Fact]
    public void EnterExitOnly_WhenDcaPaused_ChangesStatusToExitOnly()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), TradeDirection.Long);
        
        cycle.PauseDca();
        cycle.EnterExitOnly();
        
        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
    }
}
