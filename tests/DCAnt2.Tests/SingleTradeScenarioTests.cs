using System;
using System.IO;
using System.Linq;
using DCAnt2.Core.Domain;
using DCAnt2.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DCAnt2.Tests;

/// <summary>
/// Интеграционные (Scenario) тесты для полного жизненного цикла одиночной защищенной сделки (Этап 8)
/// </summary>
public class SingleTradeScenarioTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly MigrationRunner _runner;
    private readonly SqliteTradingStateStore _store;
    private readonly InstrumentRules _rules;

    public SingleTradeScenarioTests()
    {
        _dbPath = Path.GetTempFileName();
        _connectionString = $"Data Source={_dbPath}";
        _runner = new MigrationRunner(_connectionString);
        _runner.RunMigrations();
        _store = new SqliteTradingStateStore(_connectionString);
        _rules = new InstrumentRules("USDT", 0.01m, 1m, 10m);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public void FullTradeLifecycle_CompletesSuccessfully()
    {
        // 1. Создаем цикл и стартуем сделку
        var cycleId = TradeCycleId.New();
        var cycle = new TradeCycle(cycleId, 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));

        // Проверяем Outbox и сохраняем стейт
        Assert.Single(cycle.Outbox);
        var entryIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        Assert.Equal(OrderPurpose.FirstOrder, entryIntent.Purpose);
        
        _store.SaveStateAndIntents(cycle, cycle.Outbox.ToList());
        cycle.ClearOutbox();

        // 2. Симулируем получение Execution от биржи (ордер исполнен)
        var entryExecutionId = new ExecutionId("exec-entry");
        var entryExecEvent = new OrderExecuted(entryExecutionId, entryIntent.OrderId, new Price(100m), new Quantity(1m));
        
        cycle.Handle(entryExecEvent);

        // Проверяем, что позиция открыта и выставлен TP
        Assert.Equal(1m, cycle.PositionQuantity.Value);
        Assert.Equal(100m, cycle.PositionVwap.Value);
        Assert.Equal(TradeCycleStatus.Active, cycle.Status);

        Assert.Single(cycle.Outbox);
        var tpIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        Assert.Equal(OrderPurpose.TakeProfit, tpIntent.Purpose);
        Assert.Equal(102m, tpIntent.Price.Value);

        // Сохраняем состояние с исполнением
        _store.SaveStateAndIntents(cycle, cycle.Outbox.ToList(), entryExecEvent);
        cycle.ClearOutbox();

        // 3. Симулируем получение Execution по TP ордеру
        var tpExecutionId = new ExecutionId("exec-tp");
        var tpExecEvent = new OrderExecuted(tpExecutionId, tpIntent.OrderId, tpIntent.Price, tpIntent.Quantity);

        cycle.Handle(tpExecEvent);

        // Проверяем, что цикл успешно завершен
        Assert.Equal(0m, cycle.PositionQuantity.Value);
        Assert.Equal(TradeCycleStatus.Completed, cycle.Status);
        Assert.Empty(cycle.Outbox);

        _store.SaveStateAndIntents(cycle, cycle.Outbox.ToList(), tpExecEvent);
    }
    
    [Fact]
    public void EntryRejection_EntersExitOnly()
    {
        // 1. Создаем цикл и стартуем сделку
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var entryIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.ClearOutbox();
        
        // 2. Симулируем отказ от биржи
        var rejectEvent = new OrderRejected(entryIntent.OrderId, "Insufficient funds");
        cycle.Handle(rejectEvent);
        
        // 3. Проверяем, что бот перешел в ExitOnly и больше ничего не планирует
        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
        Assert.Empty(cycle.Outbox);
    }
}
