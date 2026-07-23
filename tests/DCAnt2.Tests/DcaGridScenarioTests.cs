using System;
using System.Linq;
using DCAnt2.Core.Domain;
using Xunit;

namespace DCAnt2.Tests;

public class DcaGridScenarioTests
{
    private readonly InstrumentRules _rules = new("USDT", 0.01m, 0.01m, 10m);
    
    [Fact]
    public void GridWindow_ShouldMaintainActiveOrders_And_ScaleVolume()
    {
        // 5 total grid levels, 3 active window
        var settings = new DcaSettings(1m, 2m, 2.0m, 5, 3, 3m);
        var cycle = new TradeCycle(TradeCycleId.New(), settings, _rules, OrderSide.Buy);

        cycle.Start(new Price(100m), new Quantity(1m));

        // Start() creates FirstOrder + Refills Grid
        // FirstOrder is 1, plus 2 Active DCA = 3 total outbox items
        Assert.Equal(3, cycle.Outbox.Count);
        
        var intents = cycle.Outbox.OfType<PlaceOrderIntent>().ToList();
        Assert.Equal(3, intents.Count);
        
        Assert.Equal(OrderPurpose.FirstOrder, intents[0].Purpose);
        Assert.Equal(new Price(100m), intents[0].Price);
        Assert.Equal(new Quantity(1m), intents[0].Quantity);

        Assert.Equal(OrderPurpose.DcaOrder, intents[1].Purpose);
        Assert.Equal(new Price(98m), intents[1].Price); // 100 - 2%
        Assert.Equal(new Quantity(2m), intents[1].Quantity); // 1m * 2.0^1

        Assert.Equal(OrderPurpose.DcaOrder, intents[2].Purpose);
        Assert.Equal(new Price(96m), intents[2].Price); // 100 - 4%
        Assert.Equal(new Quantity(4m), intents[2].Quantity); // 1m * 2.0^2

        cycle.ClearOutbox();

        // Fill FirstOrder
        cycle.Handle(new OrderExecuted(new ExecutionId("exec1"), intents[0].OrderId, new Price(100m), new Quantity(1m)));
        
        // VWAP should be 100, Position 1
        Assert.Equal(100m, cycle.PositionVwap.Value);
        Assert.Equal(1m, cycle.PositionQuantity.Value);

        // Window shifted! We should have 1 new outbox item: DCA3
        var newIntents = cycle.Outbox.OfType<PlaceOrderIntent>().ToList();
        Assert.Single(newIntents);
        Assert.Equal(OrderPurpose.DcaOrder, newIntents[0].Purpose);
        Assert.Equal(new Price(94m), newIntents[0].Price); // 100 - 6%
        Assert.Equal(new Quantity(8m), newIntents[0].Quantity); // 1m * 2.0^3
    }

    [Fact]
    public void Buffer_ShouldNotReplaceTakeProfit_UntilTick()
    {
        var settings = new DcaSettings(1m, 2m, 1.0m, 5, 2, 3m);
        var cycle = new TradeCycle(TradeCycleId.New(), settings, _rules, OrderSide.Buy);

        cycle.Start(new Price(100m), new Quantity(1m));
        var intents = cycle.Outbox.OfType<PlaceOrderIntent>().ToList();
        cycle.ClearOutbox();

        // 1. Partial fill #1
        cycle.Handle(new OrderExecuted(new ExecutionId("exec2"), intents[0].OrderId, new Price(100m), new Quantity(0.5m)));
        
        // No TP should be placed yet!
        Assert.DoesNotContain(cycle.Outbox, i => i is PlaceOrderIntent p && p.Purpose == OrderPurpose.TakeProfit);

        // 2. Partial fill #2
        cycle.Handle(new OrderExecuted(new ExecutionId("exec3"), intents[0].OrderId, new Price(100m), new Quantity(0.5m)));
        
        // Still no TP!
        Assert.DoesNotContain(cycle.Outbox, i => i is PlaceOrderIntent p && p.Purpose == OrderPurpose.TakeProfit);

        // 3. Tick arrives!
        cycle.Handle(new TickMessage(DateTime.UtcNow));

        // NOW TP should be placed
        var tpIntents = cycle.Outbox.OfType<PlaceOrderIntent>().Where(i => i.Purpose == OrderPurpose.TakeProfit).ToList();
        Assert.Single(tpIntents);
        Assert.Equal(new Price(101m), tpIntents[0].Price); // 100 + 1%
        
        cycle.ClearOutbox();
        
        // 4. Tick arrives again without VWAP changes
        cycle.Handle(new TickMessage(DateTime.UtcNow));
        
        // Should NOT place a new TP
        Assert.Empty(cycle.Outbox);
    }
}
