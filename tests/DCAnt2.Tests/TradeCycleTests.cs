using DCAnt2.Core.Domain;
using Xunit;
using System.Linq;

namespace DCAnt2.Tests;

public class TradeCycleTests
{
    private readonly InstrumentRules _rules = new InstrumentRules("USDT", 0.01m, 0.01m, 1m);

    [Fact]
    public void Start_CreatesFirstOrderIntent()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));

        Assert.Single(cycle.Outbox);
        var intent = (PlaceOrderIntent)cycle.Outbox[0];
        Assert.NotNull(intent);
        Assert.Equal(OrderPurpose.FirstOrder, intent.Purpose);
        Assert.Equal(OrderSide.Buy, intent.Side);
        Assert.Equal(100m, intent.Price.Value);
        Assert.Equal(1m, intent.Quantity.Value);
        Assert.Equal(TradeCycleStatus.Active, cycle.Status);
    }

    [Fact]
    public void Handle_FirstOrderExecuted_UpdatesPositionAndPlacesTakeProfit()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        
        var startIntent = (PlaceOrderIntent)cycle.Outbox[0];
        cycle.ClearOutbox();

        cycle.Handle(new OrderExecuted("exec1", startIntent.OrderId, new Price(100m), new Quantity(1m)));

        Assert.Equal(1m, cycle.PositionQuantity.Value);
        Assert.Equal(100m, cycle.PositionVwap.Value);
        
        Assert.Single(cycle.Outbox);
        var tpIntent = (PlaceOrderIntent)cycle.Outbox[0];
        Assert.NotNull(tpIntent);
        Assert.Equal(OrderPurpose.TakeProfit, tpIntent.Purpose);
        Assert.Equal(OrderSide.Sell, tpIntent.Side); // TP for Buy is Sell
        Assert.Equal(102m, tpIntent.Price.Value); // 100 + 2%
        Assert.Equal(1m, tpIntent.Quantity.Value);
    }

    [Fact]
    public void Handle_DcaOrderExecuted_UpdatesVwapAndMovesTakeProfit()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderExecuted("exec1", startIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.ClearOutbox();

        cycle.PlaceDca(new Price(90m), new Quantity(1m));
        var dcaIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.ClearOutbox();

        cycle.Handle(new OrderExecuted("exec2", dcaIntent.OrderId, new Price(90m), new Quantity(1m)));

        Assert.Equal(2m, cycle.PositionQuantity.Value);
        Assert.Equal(95m, cycle.PositionVwap.Value); // (100*1 + 90*1)/2
        
        // Outbox should contain Cancel TP and new Place TP
        Assert.Equal(2, cycle.Outbox.Count);
        Assert.IsType<CancelOrderIntent>(cycle.Outbox[0]);
        var tpIntent = (PlaceOrderIntent)cycle.Outbox[1];
        Assert.NotNull(tpIntent);
        Assert.Equal(OrderPurpose.TakeProfit, tpIntent.Purpose);
        // TP price = 95 + 2% = 96.90
        Assert.Equal(96.90m, tpIntent.Price.Value);
        Assert.Equal(2m, tpIntent.Quantity.Value);
    }

    [Fact]
    public void Handle_OrderRejected_EntersExitOnly()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderExecuted("exec1", startIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.ClearOutbox();

        cycle.PlaceDca(new Price(90m), new Quantity(1m));
        var dcaIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        
        cycle.Handle(new OrderRejected(dcaIntent.OrderId, "Insufficient funds"));

        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
    }

    [Fact]
    public void PlaceDca_WhenExitOnly_DoesNothing()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderExecuted("exec1", startIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.ClearOutbox();

        cycle.PlaceDca(new Price(90m), new Quantity(1m));
        var dcaIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderRejected(dcaIntent.OrderId, "Insufficient funds"));
        cycle.ClearOutbox();

        // Already in ExitOnly, shouldn't place DCA
        cycle.PlaceDca(new Price(80m), new Quantity(2m));
        
        Assert.Empty(cycle.Outbox);
    }

    [Fact]
    public void Handle_TakeProfitExecuted_CompletesCycle()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.ClearOutbox();
        
        cycle.Handle(new OrderExecuted("exec1", startIntent.OrderId, new Price(100m), new Quantity(1m)));
        
        var tpIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single(x => x.Purpose == OrderPurpose.TakeProfit);
        
        cycle.Handle(new OrderExecuted("exec2", tpIntent.OrderId, tpIntent.Price, tpIntent.Quantity));
        
        Assert.Equal(TradeCycleStatus.Completed, cycle.Status);
        Assert.Equal(0m, cycle.PositionQuantity.Value);
    }
}
