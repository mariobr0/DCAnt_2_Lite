using System;
using System.Collections.Generic;
using System.Text.Json;
using Dapper;
using DCAnt2.Core.Domain;
using Microsoft.Data.Sqlite;

namespace DCAnt2.Infrastructure.Database;

public class SqliteTradingStateStore : ITradingStateStore
{
    private readonly string _connectionString;

    public SqliteTradingStateStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void SaveStateAndIntents(TradeCycle cycle, IEnumerable<TradeIntent> intents, OrderExecuted? execution = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Upsert TradeCycle
            var snapshotJson = JsonSerializer.Serialize(cycle.GetSnapshot());
            connection.Execute(@"
                INSERT INTO TradeCycles (Id, Status, TotalVolume, Vwap, TotalCommission, StateSnapshot)
                VALUES (@Id, @Status, @TotalVolume, @Vwap, @TotalCommission, @StateSnapshot)
                ON CONFLICT(Id) DO UPDATE SET
                    Status = @Status,
                    TotalVolume = @TotalVolume,
                    Vwap = @Vwap,
                    TotalCommission = @TotalCommission,
                    StateSnapshot = @StateSnapshot;
            ", new
            {
                Id = cycle.Id.Value,
                Status = cycle.Status.ToString(),
                TotalVolume = cycle.PositionQuantity.Value.ToString(),
                Vwap = cycle.PositionVwap.Value.ToString(),
                TotalCommission = "0",
                StateSnapshot = snapshotJson
            }, transaction);

            foreach (var intent in intents)
            {
                var payload = JsonSerializer.Serialize((object)intent);
                var type = intent switch
                {
                    PlaceOrderIntent => "PlaceOrder",
                    CancelOrderIntent => "CancelOrder",
                    _ => intent.GetType().Name
                };

                if (intent is PlaceOrderIntent placeIntent)
                {
                    connection.Execute(@"
                        INSERT INTO ManagedOrders (InternalOrderId, TradeCycleId, Type, Status, Price, Quantity)
                        VALUES (@InternalOrderId, @TradeCycleId, @Type, @Status, @Price, @Quantity)
                        ON CONFLICT(InternalOrderId) DO NOTHING;
                    ", new
                    {
                        InternalOrderId = placeIntent.OrderId.Value,
                        TradeCycleId = cycle.Id.Value,
                        Type = placeIntent.Purpose.ToString(),
                        Status = "Pending",
                        Price = placeIntent.Price.Value.ToString(),
                        Quantity = placeIntent.Quantity.Value.ToString()
                    }, transaction);
                }
                
                if (intent is CancelOrderIntent cancelIntent)
                {
                    connection.Execute(@"
                        UPDATE ManagedOrders SET Status = 'CancelPending' WHERE InternalOrderId = @OrderId;
                    ", new { OrderId = cancelIntent.OrderId.Value }, transaction);
                }

                connection.Execute(@"
                    INSERT INTO Outbox (TradeCycleId, IntentType, Payload, CreatedAt)
                    VALUES (@TradeCycleId, @IntentType, @Payload, @CreatedAt);
                ", new
                {
                    TradeCycleId = cycle.Id.Value,
                    IntentType = type,
                    Payload = payload,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }, transaction);
            }

            if (execution != null)
            {
                connection.Execute(@"
                    INSERT INTO Executions (ExecutionId, InternalOrderId, TradeCycleId, Price, Quantity, Commission, Timestamp)
                    VALUES (@ExecutionId, @InternalOrderId, @TradeCycleId, @Price, @Quantity, @Commission, @Timestamp);
                ", new
                {
                    ExecutionId = execution.ExecutionId.Value,
                    InternalOrderId = execution.OrderId.Value,
                    TradeCycleId = cycle.Id.Value,
                    Price = execution.ExecutedPrice.Value.ToString(),
                    Quantity = execution.ExecutedQuantity.Value.ToString(),
                    Commission = "0",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }, transaction);

                connection.Execute(@"
                    UPDATE ManagedOrders SET Status = 'Filled' WHERE InternalOrderId = @OrderId;
                ", new { OrderId = execution.OrderId.Value }, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public TradeCycle? LoadActiveCycle(InstrumentRules rules, DcaSettings settings)
    {
        using var connection = new SqliteConnection(_connectionString);
        
        var cycleRow = connection.QueryFirstOrDefault(@"
            SELECT Id, Status, TotalVolume, Vwap, StateSnapshot 
            FROM TradeCycles 
            WHERE Status != 'Completed'
            ORDER BY rowid DESC LIMIT 1
        ");

        if (cycleRow == null) return null;

        var snapshot = JsonSerializer.Deserialize<TradeCycleSnapshot>((string)cycleRow.StateSnapshot);
        if (snapshot == null) return null;

        var cycleId = new TradeCycleId((string)cycleRow.Id);
        // Assuming OrderSide Buy for now, or we can add it to snapshot later if we support shorting
        var cycle = new TradeCycle(cycleId, settings, rules, OrderSide.Buy);
        
        cycle.RestoreSnapshot(snapshot);
        
        // Restore Position
        cycle.RestorePosition(
            new Quantity(decimal.Parse((string)cycleRow.TotalVolume)),
            new Price(decimal.Parse((string)cycleRow.Vwap))
        );

        // Load active managed orders
        var managedOrders = connection.Query(@"
            SELECT InternalOrderId, Type 
            FROM ManagedOrders 
            WHERE TradeCycleId = @Id AND Status IN ('Pending', 'Filled')
        ", new { Id = cycleRow.Id });

        foreach (var row in managedOrders)
        {
            var internalId = new InternalOrderId((string)row.InternalOrderId);
            if (Enum.TryParse<OrderPurpose>((string)row.Type, out var purpose))
            {
                cycle.RestoreOrder(internalId, purpose);
            }
        }

        return cycle;
    }

    public bool IsExecutionProcessed(string executionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var count = connection.QuerySingleOrDefault<int>("SELECT COUNT(1) FROM Executions WHERE ExecutionId = @Id", new { Id = executionId });
        return count > 0;
    }
}
