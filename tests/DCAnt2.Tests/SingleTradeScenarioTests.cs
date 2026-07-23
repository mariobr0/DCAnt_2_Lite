using System;
using System.Linq;
using DCAnt2.Core.Domain;
using Xunit;

namespace DCAnt2.Tests;

public class SingleTradeScenarioTests
{
    private readonly InstrumentRules _rules = new InstrumentRules("USDT", 0.01m, 1m, 10m);

    [Fact]
    public void FullTradeLifecycle_CompletesSuccessfully()
    {
        // 1. Ð¡Ð¾Ð·Ð´Ð°ÐµÐ¼ Ñ†Ð¸ÐºÐ» Ð¸ ÑÑ‚Ð°Ñ€Ñ‚ÑƒÐµÐ¼ ÑÐ´ÐµÐ»ÐºÑƒ
        var cycleId = TradeCycleId.New();
        var settings = new DcaSettings(2m, 2m, 1m, 0, 0, 3m);
        var cycle = new TradeCycle(cycleId, settings, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));

        // ÐŸÑ€Ð¾Ð²ÐµÑ€ÑÐµÐ¼ Outbox Ð¸ ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÐµÐ¼ ÑÑ‚ÐµÐ¹Ñ‚
        var entryIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        Assert.Equal(OrderPurpose.FirstOrder, entryIntent.Purpose);
        cycle.ClearOutbox();

        // 2. Ð˜ÑÐ¿Ð¾Ð»Ð½ÐµÐ½Ð¸Ðµ Ð²Ñ…Ð¾Ð´Ð° -> Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð²Ñ‹ÑÑ‚Ð°Ð²Ð¸Ñ‚ÑŒÑÑ Ñ‚ÐµÐ¹Ðº-Ð¿Ñ€Ð¾Ñ„Ð¸Ñ‚ Ð½Ð° ÑÐ»ÐµÐ´ÑƒÑŽÑ‰ÐµÐ¼ Tick
        cycle.Handle(new OrderExecuted(new ExecutionId("exec-1"), entryIntent.OrderId, new Price(100m), new Quantity(1m)));
        
        cycle.Handle(new TickMessage(DateTime.UtcNow));
        var tpIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single(i => i.Purpose == OrderPurpose.TakeProfit);
        Assert.Equal(OrderPurpose.TakeProfit, tpIntent.Purpose);
        Assert.Equal(102m, tpIntent.Price.Value);
        
        var slIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single(i => i.Purpose == OrderPurpose.StopLoss);
        Assert.NotNull(slIntent);
        cycle.ClearOutbox();

        // 3. Ð˜ÑÐ¿Ð¾Ð»Ð½ÐµÐ½Ð¸Ðµ Ñ‚ÐµÐ¹Ðº-Ð¿Ñ€Ð¾Ñ„Ð¸Ñ‚Ð° -> Ð·Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ð¸Ðµ Ñ†Ð¸ÐºÐ»Ð°
        cycle.Handle(new OrderExecuted(new ExecutionId("exec-2"), tpIntent.OrderId, new Price(102m), new Quantity(1m)));
        
        Assert.Equal(TradeCycleStatus.Completed, cycle.Status);
        Assert.Equal(TradeCycleExitReason.TakeProfit, cycle.ExitReason);
        Assert.Equal(0m, cycle.PositionQuantity.Value);
    }

    [Fact]
    public void EntryRejection_EntersExitOnly()
    {
        // 1. Ð¡Ð¾Ð·Ð´Ð°ÐµÐ¼ Ñ†Ð¸ÐºÐ» Ð¸ ÑÑ‚Ð°Ñ€Ñ‚ÑƒÐµÐ¼ ÑÐ´ÐµÐ»ÐºÑƒ
        var settings = new DcaSettings(2m, 2m, 1m, 0, 0, 3m);
        var cycle = new TradeCycle(TradeCycleId.New(), settings, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var entryIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.ClearOutbox();
        
        // 2. Ð¡Ð¸Ð¼ÑƒÐ»Ð¸Ñ€ÑƒÐµÐ¼ Ð¾Ñ‚ÐºÐ°Ð· Ð¾Ñ‚ Ð±Ð¸Ñ€Ð¶Ð¸
        var rejectEvent = new OrderRejected(entryIntent.OrderId, "Insufficient funds");
        cycle.Handle(rejectEvent);

        // 3. ÐŸÑ€Ð¾Ð²ÐµÑ€ÑÐµÐ¼ Ð¿ÐµÑ€ÐµÑ…Ð¾Ð´ Ð² ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð´Ð»Ñ Ð²Ñ‹Ñ…Ð¾Ð´Ð° (Ð² Ð´Ð°Ð½Ð½Ð¾Ð¼ ÑÐ»ÑƒÑ‡Ð°Ðµ Ð²Ñ‹Ñ…Ð¾Ð´ = Ð½Ð¸Ñ‡ÐµÐ³Ð¾ Ð½Ðµ Ð´ÐµÐ»Ð°Ñ‚ÑŒ, Ñ‚Ð°Ðº ÐºÐ°Ðº Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ Ð½ÐµÑ‚)
        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
        Assert.Empty(cycle.Outbox);
    }
}
