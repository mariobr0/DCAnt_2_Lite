using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;
using DCAnt2.Core.Domain;
using DCAnt2.Infrastructure.Database;

namespace DCAnt2.Tests;

public class SqliteTradingStateStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly MigrationRunner _runner;
    private readonly SqliteTradingStateStore _store;

    public SqliteTradingStateStoreTests()
    {
        _dbPath = Path.GetTempFileName();
        _connectionString = $"Data Source={_dbPath}";
        _runner = new MigrationRunner(_connectionString);
        _runner.RunMigrations();
        _store = new SqliteTradingStateStore(_connectionString);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public void SaveStateAndIntents_SavesCycleAndOutboxTransactionally()
    {
        // Arrange
        var rules = new InstrumentRules("USDT", 0.01m, 1m, 10m);
        var settings = new DcaSettings(2m, 2m, 1m, 1, 1, 3m);
        var cycle = new TradeCycle(TradeCycleId.New(), settings, rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));

        var intents = cycle.Outbox.ToList();
        
        // Act
        _store.SaveStateAndIntents(cycle, intents);

        // Assert
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM TradeCycles WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", cycle.Id.Value);
        var cycleCount = (long)cmd.ExecuteScalar()!;
        Assert.Equal(1, cycleCount);

        cmd.CommandText = "SELECT COUNT(*) FROM Outbox WHERE TradeCycleId = @id";
        var outboxCount = (long)cmd.ExecuteScalar()!;
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public void SaveStateAndIntents_WithDuplicateExecution_ThrowsExceptionAndRollsBack()
    {
        // Arrange
        var rules = new InstrumentRules("USDT", 0.01m, 1m, 10m);
        var settings = new DcaSettings(2m, 2m, 1m, 1, 1, 3m);
        var cycle = new TradeCycle(TradeCycleId.New(), settings, rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        
        var intents = cycle.Outbox.ToList();
        var execId = new ExecutionId("exec123");
        var exec = new OrderExecuted(execId, intents.OfType<PlaceOrderIntent>().First().OrderId, new Price(100), new Quantity(1));

        // Act
        // 1. Initial save
        _store.SaveStateAndIntents(cycle, intents, exec);

        // 2. Modify state and try saving same execution again
        cycle.Handle(exec);
        var newIntents = cycle.Outbox.ToList();

        // Assert
        Assert.Throws<SqliteException>(() => _store.SaveStateAndIntents(cycle, newIntents, exec));

        // Verify transaction rolled back (no new outbox items)
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Outbox WHERE TradeCycleId = @id";
        cmd.Parameters.AddWithValue("@id", cycle.Id.Value);
        var outboxCount = (long)cmd.ExecuteScalar()!;
        Assert.Equal(1, outboxCount); // Should still be 1 from the first save, not 2
    }

    [Fact]
    public void IsExecutionProcessed_ReturnsTrue_IfSaved()
    {
        // Arrange
        var rules = new InstrumentRules("USDT", 0.01m, 1m, 10m);
        var settings = new DcaSettings(2m, 2m, 1m, 1, 1, 3m);
        var cycle = new TradeCycle(TradeCycleId.New(), settings, rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var intents = cycle.Outbox.ToList();
        var exec = new OrderExecuted(new ExecutionId("exec-xyz"), intents.OfType<PlaceOrderIntent>().First().OrderId, new Price(100), new Quantity(1));

        // Act & Assert
        Assert.False(_store.IsExecutionProcessed("exec-xyz"));
        
        _store.SaveStateAndIntents(cycle, intents, exec);

        Assert.True(_store.IsExecutionProcessed("exec-xyz"));
    }

    [Fact]
    public void LoadActiveCycle_RestoresCompleteState()
    {
        var dbPath = $"test_{Guid.NewGuid()}.db";
        var connStr = $"Data Source={dbPath}";
        var runner = new MigrationRunner(connStr);
        runner.RunMigrations();

        var store = new SqliteTradingStateStore(connStr);
        var rules = new InstrumentRules("USDT", 0.01m, 1m, 10m);
        var settings = new DcaSettings(2m, 2m, 1.5m, 5, 2, 3m);
        
        var originalCycle = new TradeCycle(TradeCycleId.New(), settings, rules, OrderSide.Buy);
        originalCycle.Start(new Price(100m), new Quantity(1m));
        
        var intents = originalCycle.Outbox.ToList();
        originalCycle.ClearOutbox();
        
        var firstIntent = intents.OfType<PlaceOrderIntent>().First(i => i.Purpose == OrderPurpose.FirstOrder);
        
        // Fill first order
        var exec = new OrderExecuted(new ExecutionId("e1"), firstIntent.OrderId, new Price(100m), new Quantity(1m));
        originalCycle.Handle(exec);
        
        // Save
        store.SaveStateAndIntents(originalCycle, intents, exec);
        originalCycle.ClearOutbox();

        // Restore
        var store2 = new SqliteTradingStateStore(connStr);
        var restoredCycle = store2.LoadActiveCycle(rules, settings);

        Assert.NotNull(restoredCycle);
        Assert.Equal(originalCycle.Id.Value, restoredCycle.Id.Value);
        Assert.Equal(1m, restoredCycle.PositionQuantity.Value);
        Assert.Equal(100m, restoredCycle.PositionVwap.Value);
        
        var snapshot1 = originalCycle.GetSnapshot();
        var snapshot2 = restoredCycle.GetSnapshot();
        
        Assert.Equal(snapshot1.GeneratedGridLevels, snapshot2.GeneratedGridLevels);
        Assert.Equal(snapshot1.ActiveGridOrdersCount, snapshot2.ActiveGridOrdersCount);
        
        // Clean up
        if (System.IO.File.Exists(dbPath))
        {
            try { System.IO.File.Delete(dbPath); } catch { /* Ignore */ }
        }
    }
}
