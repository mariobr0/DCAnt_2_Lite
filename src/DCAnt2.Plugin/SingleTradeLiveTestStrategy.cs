using System;
using System.IO;
using System.Linq;
using DCAnt2.Core.Domain;
using DCAnt2.Infrastructure.Database;
using DCAnt2.Quantower;
using TradingPlatform.BusinessLayer;

namespace DCAnt2.Plugin
{
    public class SingleTradeLiveTestStrategy : Strategy
    {
        [InputParameter("Test Symbol", 0)]
        public Symbol TestSymbol = default!;

        [InputParameter("Test Account", 1)]
        public Account TestAccount = default!;

        private SqliteTradingStateStore _store = default!;
        private EngineLoop _engineLoop = default!;
        private QuantowerAdapter _adapter = default!;
        private TradeCycle _cycle = default!;
        private string _dbPath = default!;

        public SingleTradeLiveTestStrategy()
        {
            Name = "DCAnt2 Live Test 120620";
            Description = "Executes one protected trade (Entry + TP) for Stage 8.5 tests.";
        }

        protected override void OnCreated()
        {
            // Only basic properties here. Heavy initialization goes to OnRun.
        }

        protected override void OnRun()
        {
            if (TestSymbol == null || TestAccount == null)
            {
                Log("Symbol or Account is not selected.", StrategyLoggingLevel.Error);
                return;
            }

            // --- INIT COMPONENTS ---
            _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DCAnt2_LiveTest.db");
            var connString = $"Data Source={_dbPath}";
            
            // Explicitly initialize SQLite provider in plugin context
            SQLitePCL.Batteries.Init();
            
            var runner = new MigrationRunner(connString);
            runner.RunMigrations();
            
            _store = new SqliteTradingStateStore(connString);
            
            _engineLoop = new EngineLoop(
                messageHandler: msg =>
                {
                    if (_cycle == null) return;

                    Log($"[EngineLoop] Received {msg.GetType().Name}", StrategyLoggingLevel.Info);
                    
                    if (msg is ExecutionMessage execMsg)
                    {
                        _cycle.Handle(execMsg.Execution);
                    }
                    else if (msg is RejectionMessage rejMsg)
                    {
                        _cycle.Handle(rejMsg.Rejection);
                    }
                    
                    var intents = _cycle.Outbox.ToList();
                    if (intents.Any())
                    {
                        Log($"[EngineLoop] Processing {intents.Count} intents...", StrategyLoggingLevel.Info);
                        
                        _adapter.ProcessOutbox(intents);
                        
                        // Save to DB
                        if (msg is ExecutionMessage execMsgForStore)
                            _store.SaveStateAndIntents(_cycle, intents, execMsgForStore.Execution);
                        else
                            _store.SaveStateAndIntents(_cycle, intents);
                            
                        _cycle.ClearOutbox();
                    }
                },
                onPanic: ex =>
                {
                    Log($"[EngineLoop] PANIC: {ex.Message}", StrategyLoggingLevel.Error);
                }
            );

            _engineLoop.Start();
            
            _adapter = new QuantowerAdapter(_engineLoop, TestAccount, TestSymbol);
            TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded += OnOrdersHistoryAdded;

            Log($"=== Starting Single Trade Live Test ===", StrategyLoggingLevel.Info);
            Log($"Database: {_dbPath}", StrategyLoggingLevel.Info);

            var rules = new InstrumentRules(
                "USDT",
                (decimal)TestSymbol.TickSize,
                (decimal)TestSymbol.MinLot,
                10m // MinNotional
            );

            _cycle = new TradeCycle(TradeCycleId.New(), 1m, rules, OrderSide.Buy);
            
            // Start price slightly below market
            var entryPrice = new DCAnt2.Core.Domain.Price((decimal)(TestSymbol.Bid - (TestSymbol.TickSize * 10)));
            var entryQty = new Quantity((decimal)TestSymbol.MinLot);
            
            // Basic test check to avoid duplicate orders on restart
            var activeOrder = TradingPlatform.BusinessLayer.Core.Instance.Orders
                .FirstOrDefault(o => o.Symbol == TestSymbol && o.Account == TestAccount && (o.Status == OrderStatus.Opened || o.Status == OrderStatus.PartiallyFilled));

            var activePosition = TradingPlatform.BusinessLayer.Core.Instance.Positions
                .FirstOrDefault(p => p.Symbol == TestSymbol && p.Account == TestAccount && p.Quantity != 0);

            if (activeOrder != null)
            {
                Log($"Found active order {activeOrder.Id}. Simulating reconciliation...", StrategyLoggingLevel.Info);
                var orderId = new InternalOrderId(activeOrder.Comment);
                _adapter.Reconcile(new[] { orderId });
            }
            else if (activePosition != null)
            {
                Log($"Found active position. Assuming TP is pending...", StrategyLoggingLevel.Info);
            }
            else
            {
                Log($"Starting TradeCycle: Entry Price {entryPrice.Value}, Qty {entryQty.Value}", StrategyLoggingLevel.Info);
                _cycle.Start(entryPrice, entryQty);
                
                var intents = _cycle.Outbox.ToList();
                if (intents.Any())
                {
                    Log($"Dispatching Initial Order...", StrategyLoggingLevel.Info);
                    _adapter.ProcessOutbox(intents);
                    _store.SaveStateAndIntents(_cycle, intents);
                    _cycle.ClearOutbox();
                }
            }
        }
        
        private void OnOrdersHistoryAdded(OrderHistory order)
        {
            if (_adapter != null && order != null)
                _adapter.HandleOrderHistoryAdded(order);
        }

        protected override void OnStop()
        {
            Log("Stopping Live Test...", StrategyLoggingLevel.Info);
            
            TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded -= OnOrdersHistoryAdded;
            if (_engineLoop != null)
            {
                _engineLoop.StopAsync().Wait();
                _engineLoop.Dispose();
            }
        }
    }
}
