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
            connection.Execute(@"
                INSERT INTO TradeCycles (Id, Status, TotalVolume, Vwap, TotalCommission)
                VALUES (@Id, @Status, @TotalVolume, @Vwap, @TotalCommission)
                ON CONFLICT(Id) DO UPDATE SET
                    Status = @Status,
                    TotalVolume = @TotalVolume,
                    Vwap = @Vwap,
                    TotalCommission = @TotalCommission;
            ", new
            {
                Id = cycle.Id.Value,
                Status = cycle.Status.ToString(),
                TotalVolume = cycle.PositionQuantity.Value.ToString(),
                Vwap = cycle.PositionVwap.Value.ToString(),
                TotalCommission = "0"
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

    public TradeCycle? LoadActiveCycle()
    {
        throw new NotImplementedException("To be implemented in stage 9 (Recovery)");
    }

    public bool IsExecutionProcessed(string executionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var count = connection.QuerySingleOrDefault<int>("SELECT COUNT(1) FROM Executions WHERE ExecutionId = @Id", new { Id = executionId });
        return count > 0;
    }
}
