using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DCAnt2.Infrastructure.Database;

public class MigrationRunner
{
    private readonly string _connectionString;

    public MigrationRunner(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void RunMigrations()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Ensure SchemaMigrations table exists
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS SchemaMigrations (
                Version INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                AppliedAt INTEGER NOT NULL
            );
        ");

        var appliedMigrations = connection.Query<int>("SELECT Version FROM SchemaMigrations").ToHashSet();

        foreach (var migration in GetMigrations())
        {
            if (!appliedMigrations.Contains(migration.Version))
            {
                using var transaction = connection.BeginTransaction();
                try
                {
                    connection.Execute(migration.Sql, transaction: transaction);
                    connection.Execute(
                        "INSERT INTO SchemaMigrations (Version, Name, AppliedAt) VALUES (@Version, @Name, @AppliedAt)",
                        new { migration.Version, migration.Name, AppliedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                        transaction: transaction
                    );
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    private IEnumerable<(int Version, string Name, string Sql)> GetMigrations()
    {
        yield return (1, "InitialSchema", @"
            CREATE TABLE TradeCycles (
                Id TEXT PRIMARY KEY,
                Status TEXT NOT NULL,
                TotalVolume TEXT NOT NULL,
                Vwap TEXT NOT NULL,
                TotalCommission TEXT NOT NULL
            );

            CREATE TABLE ManagedOrders (
                InternalOrderId TEXT PRIMARY KEY,
                TradeCycleId TEXT NOT NULL,
                Type TEXT NOT NULL,
                Status TEXT NOT NULL,
                Price TEXT NOT NULL,
                Quantity TEXT NOT NULL,
                FOREIGN KEY (TradeCycleId) REFERENCES TradeCycles(Id)
            );

            CREATE TABLE Executions (
                ExecutionId TEXT PRIMARY KEY,
                InternalOrderId TEXT NOT NULL,
                TradeCycleId TEXT NOT NULL,
                Price TEXT NOT NULL,
                Quantity TEXT NOT NULL,
                Commission TEXT NOT NULL,
                Timestamp INTEGER NOT NULL,
                FOREIGN KEY (InternalOrderId) REFERENCES ManagedOrders(InternalOrderId),
                FOREIGN KEY (TradeCycleId) REFERENCES TradeCycles(Id)
            );

            CREATE TABLE Outbox (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TradeCycleId TEXT NOT NULL,
                IntentType TEXT NOT NULL,
                Payload TEXT NOT NULL,
                CreatedAt INTEGER NOT NULL,
                FOREIGN KEY (TradeCycleId) REFERENCES TradeCycles(Id)
            );
        ");
    }
}
