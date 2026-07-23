using System;
using System.Linq;
using DCAnt2.Core.Domain;
using Xunit;

namespace DCAnt2.Tests;

public class TradeCycleTests
{
    private readonly InstrumentRules _rules = new InstrumentRules("USDT", 0.01m, 0.01m, 1m);
    private readonly DcaSettings _settings = new DcaSettings(2m, 2m, 1m, 5, 2, 3m); // TP=2%, Step=2%, ActiveGrid=2

    [Fact]
    public void Start_CreatesFirstOrderIntent_And_Grid()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), _settings, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));

        // Since ActiveGridWindow=2, we should get 1 FirstOrder + 1 DcaOrder = 2 intents total
        Assert.Equal(2, cycle.Outbox.Count);
        
        var firstIntent = (PlaceOrderIntent)cycle.Outbox[0];
        Assert.Equal(OrderPurpose.FirstOrder, firstIntent.Purpose);
        Assert.Equal(100m, firstIntent.Price.Value);
        Assert.Equal(1m, firstIntent.Quantity.Value);
        Assert.Equal(TradeCycleStatus.Active, cycle.Status);
    }

    [Fact]
    public void Handle_FirstOrderExecuted_UpdatesPosition_And_RequiresTick_For_ExitOrders()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), _settings, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        
        var startIntent = (PlaceOrderIntent)cycle.Outbox[0];
        cycle.ClearOutbox();

        cycle.Handle(new OrderExecuted(new ExecutionId("exec1"), startIntent.OrderId, new Price(100m), new Quantity(1m)));

        Assert.Equal(1m, cycle.PositionQuantity.Value);
        Assert.Equal(100m, cycle.PositionVwap.Value);
        
        // No exit orders placed until Tick!
        var exitIntentsBeforeTick = cycle.Outbox.OfType<PlaceOrderIntent>().Where(i => i.Purpose == OrderPurpose.TakeProfit || i.Purpose == OrderPurpose.StopLoss).ToList();
        Assert.Empty(exitIntentsBeforeTick);

        cycle.Handle(new TickMessage(DateTime.UtcNow));

        var tpIntentsAfterTick = cycle.Outbox.OfType<PlaceOrderIntent>().Where(i => i.Purpose == OrderPurpose.TakeProfit).ToList();
        Assert.Single(tpIntentsAfterTick);
        Assert.Equal(102m, tpIntentsAfterTick[0].Price.Value); // 100 + 2%
        
        var slIntentsAfterTick = cycle.Outbox.OfType<PlaceOrderIntent>().Where(i => i.Purpose == OrderPurpose.StopLoss).ToList();
        Assert.Single(slIntentsAfterTick);
        
        // Entry 100, MaxLevels 5, Step 2% => total drop 10%. Last level = 90. SL = 3% below 90 => 87.3
        Assert.Equal(87.3m, slIntentsAfterTick[0].Price.Value);
    }

    [Fact]
    public void Handle_DcaOrderExecuted_UpdatesVwap_And_ReplacesExitOrders_On_Tick()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), _settings, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var intents = cycle.Outbox.OfType<PlaceOrderIntent>().ToList();
        cycle.ClearOutbox();

        var firstIntent = intents[0];
        var dcaIntent = intents[1];

        cycle.Handle(new OrderExecuted(new ExecutionId("exec1"), firstIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.Handle(new TickMessage(DateTime.UtcNow));
        cycle.ClearOutbox(); // TP & SL were placed

        cycle.Handle(new OrderExecuted(new ExecutionId("exec2"), dcaIntent.OrderId, new Price(98m), new Quantity(1m)));

        Assert.Equal(2m, cycle.PositionQuantity.Value);
        Assert.Equal(99m, cycle.PositionVwap.Value); // (100*1 + 98*1)/2
        
        // Outbox shouldn't have new Exit orders yet (only new Grid orders)
        Assert.DoesNotContain(cycle.Outbox, i => i is PlaceOrderIntent p && (p.Purpose == OrderPurpose.TakeProfit || p.Purpose == OrderPurpose.StopLoss));

        cycle.Handle(new TickMessage(DateTime.UtcNow));

        // Outbox should contain Cancel TP & SL and new Place TP & SL
        var cancels = cycle.Outbox.OfType<CancelOrderIntent>().ToList();
        Assert.Equal(2, cancels.Count); // One for TP, one for SL

        var tpIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single(i => i.Purpose == OrderPurpose.TakeProfit);
        Assert.NotNull(tpIntent);
        
        // TP price = 99 + 2% = 100.98
        Assert.Equal(100.98m, tpIntent.Price.Value);
        Assert.Equal(2m, tpIntent.Quantity.Value);

        var slIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single(i => i.Purpose == OrderPurpose.StopLoss);
        Assert.NotNull(slIntent);
        Assert.Equal(87.3m, slIntent.Price.Value);
        Assert.Equal(2m, slIntent.Quantity.Value); // SL size updated
    }

    [Fact]
    public void Handle_OrderRejected_EntersExitOnly()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), _settings, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var intents = cycle.Outbox.OfType<PlaceOrderIntent>().ToList();
        cycle.ClearOutbox();

        var dcaIntent = intents[1];
        
        cycle.Handle(new OrderRejected(dcaIntent.OrderId, "Insufficient funds"));

        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
    }

    [Fact]
    public void Handle_TakeProfitExecuted_CompletesCycle()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), _settings, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().First();
        cycle.ClearOutbox();
        
        cycle.Handle(new OrderExecuted(new ExecutionId("exec1"), startIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.Handle(new TickMessage(DateTime.UtcNow));
        
        var tpIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single(x => x.Purpose == OrderPurpose.TakeProfit);
        
        cycle.Handle(new OrderExecuted(new ExecutionId("exec2"), tpIntent.OrderId, tpIntent.Price, tpIntent.Quantity));
        
        Assert.Equal(TradeCycleStatus.Completed, cycle.Status);
        Assert.Equal(TradeCycleExitReason.TakeProfit, cycle.ExitReason);
        Assert.Equal(0m, cycle.PositionQuantity.Value);
    }
}
