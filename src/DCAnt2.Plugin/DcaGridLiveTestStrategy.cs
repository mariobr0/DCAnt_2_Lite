using System;
using System.IO;
using System.Linq;
using DCAnt2.Core.Domain;
using DCAnt2.Infrastructure.Database;
using DCAnt2.Quantower;
using TradingPlatform.BusinessLayer;

namespace DCAnt2.Plugin
{
    public class DcaGridLiveTestStrategy : Strategy
    {
        [InputParameter("Test Symbol", 0)]
        public Symbol TestSymbol = default!;

        [InputParameter("Test Account", 1)]
        public Account TestAccount = default!;

        [InputParameter("Max Grid Levels", 2, minimum: 0, maximum: 100)]
        public int MaxGridLevels = 5;

        [InputParameter("Active Grid Window", 3, minimum: 1, maximum: 100)]
        public int ActiveGridWindow = 2;

        [InputParameter("Step Percent", 4, minimum: 0.1, maximum: 100.0, increment: 0.1, decimalPlaces: 2)]
        public double StepPercent = 2.0;

        [InputParameter("Take Profit Percent", 5, minimum: 0.1, maximum: 100.0, increment: 0.1, decimalPlaces: 2)]
        public double TpPercent = 1.0;

        [InputParameter("Volume Scale", 6, minimum: 1.0, maximum: 10.0, increment: 0.1, decimalPlaces: 2)]
        public double VolumeScale = 1.0;

        [InputParameter("Pause After TP (ms)", 7, minimum: 0, maximum: 3600000, increment: 1000)]
        public int PauseAfterTpMs = 3000;

        [InputParameter("Pause After SL (ms)", 8, minimum: 0, maximum: 3600000, increment: 1000)]
        public int PauseAfterSlMs = 60000;

        [InputParameter("Last Level Stop (%)", 9, minimum: 0.0, maximum: 100.0, increment: 0.1, decimalPlaces: 2)]
        public double LastLevelStopPercent = 3.0;

        private SqliteTradingStateStore _store = default!;
        private EngineLoop _engineLoop = default!;
        private QuantowerAdapter _adapter = default!;
        private TradeCycle _cycle = default!;
        private string _dbPath = default!;
        private System.Threading.Timer? _throttleTimer;
        private System.Threading.Timer? _restartTimer;
        private QuantowerFileLogger _logger = default!;

        public DcaGridLiveTestStrategy()
        {
            Name = "DCAnt2 Grid Test 215833";
            Description = "Executes DCA grid tests for Stage 9.";
        }

        protected override void OnCreated()
        {
            // Only basic properties here. Heavy initialization goes to OnRun.
        }

        protected override void OnRun()
        {
            _logger = new QuantowerFileLogger(this, Name, this.Log);
            
            if (TestSymbol == null || TestAccount == null)
            {
                _logger.Error("Symbol or Account is not selected.");
                return;
            }

            // --- INIT COMPONENTS ---
            var dbDir = @"C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\db";
            Directory.CreateDirectory(dbDir);
            _dbPath = Path.Combine(dbDir, "DCAnt2_GridLiveTest_v2.db");
            var connString = $"Data Source={_dbPath};Pooling=False;";
            
            // Explicitly initialize SQLite provider in plugin context
            SQLitePCL.Batteries.Init();
            
            var runner = new MigrationRunner(connString);
            runner.RunMigrations();
            
            _store = new SqliteTradingStateStore(connString);
            
            _engineLoop = new EngineLoop(
                messageHandler: msg =>
                {
                    if (msg is StartNewCycleMessage)
                    {
                        if (_restartTimer != null)
                        {
                            _restartTimer.Dispose();
                            _restartTimer = null;
                        }
                        
                        ProcessStartNewCycle();
                    }

                    if (_cycle == null) return;

                    if (!(msg is TickMessage))
                    {
                        _logger.Info($"[EngineLoop] Received {msg.GetType().Name}");
                    }
                    
                    if (msg is ExecutionMessage execMsg)
                    {
                        _cycle.Handle(execMsg.Execution);
                    }
                    else if (msg is RejectionMessage rejMsg)
                    {
                        _cycle.Handle(rejMsg.Rejection);
                    }
                    else if (msg is TickMessage tickMsg)
                    {
                        _cycle.Handle(tickMsg);
                    }
                    
                    var intents = _cycle.Outbox.ToList();
                    if (intents.Any())
                    {
                        _logger.Info($"[EngineLoop] Processing {intents.Count} intents...");
                        
                        _adapter.ProcessOutbox(intents);
                        
                        // Save to DB
                        if (msg is ExecutionMessage execMsgForStore)
                            _store.SaveStateAndIntents(_cycle, intents, execMsgForStore.Execution);
                        else
                            _store.SaveStateAndIntents(_cycle, intents);
                            
                        _cycle.ClearOutbox();
                    }

                    // Check for cycle completion
                    if (_cycle.Status == TradeCycleStatus.Completed && _restartTimer == null)
                    {
                        int pauseMs = _cycle.ExitReason == TradeCycleExitReason.TakeProfit ? PauseAfterTpMs : PauseAfterSlMs;
                        _logger.Info($"Cycle completed ({_cycle.ExitReason}). Scheduling new cycle in {pauseMs}ms...");
                        _restartTimer = new System.Threading.Timer(_ => 
                        {
                            if (_engineLoop != null)
                                _engineLoop.Enqueue(new StartNewCycleMessage());
                        }, null, pauseMs, System.Threading.Timeout.Infinite);
                    }
                    else if (_cycle.Status == TradeCycleStatus.ExitOnly && _cycle.PositionQuantity.Value == 0 && _restartTimer == null)
                    {
                        _logger.Info($"Cycle aborted (ExitOnly). Scheduling new cycle in {PauseAfterSlMs}ms...");
                        _restartTimer = new System.Threading.Timer(_ => 
                        {
                            if (_engineLoop != null)
                                _engineLoop.Enqueue(new StartNewCycleMessage());
                        }, null, PauseAfterSlMs, System.Threading.Timeout.Infinite);
                    }
                },
                onPanic: ex =>
                {
                    _logger.Error($"[EngineLoop] PANIC: {ex.Message}");
                }
            );

            _engineLoop.Start();
            
            _adapter = new QuantowerAdapter(_engineLoop, TestAccount, TestSymbol, msg => _logger.Info(msg));
            TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded += OnOrdersHistoryAdded;

            _logger.Info($"=== Starting Grid Live Test ===");
            _logger.Info($"Database: {_dbPath}");

            var rules = new InstrumentRules(
                "USDT",
                (decimal)TestSymbol.TickSize,
                (decimal)TestSymbol.MinLot,
                10m // MinNotional
            );

            var settings = new DcaSettings(
                TpPercent: (decimal)TpPercent,
                StepPercent: (decimal)StepPercent,
                VolumeScale: (decimal)VolumeScale,
                MaxGridLevels: MaxGridLevels,
                ActiveGridWindow: ActiveGridWindow,
                LastLevelStopPercent: (decimal)LastLevelStopPercent
            );

            var loadedCycle = _store.LoadActiveCycle(rules, settings);
            
            // Basic test check to avoid duplicate orders on restart
            var activeOrder = TradingPlatform.BusinessLayer.Core.Instance.Orders
                .FirstOrDefault(o => o.Symbol == TestSymbol && o.Account == TestAccount && (o.Status == OrderStatus.Opened || o.Status == OrderStatus.PartiallyFilled));

            if (loadedCycle != null)
            {
                if (activeOrder == null)
                {
                    _logger.Info($"No active orders found on exchange. Discarding old cycle from DB and starting fresh.");
                    loadedCycle = null;
                }
                else
                {
                    _cycle = loadedCycle;
                    _logger.Info($"Loaded active TradeCycle {_cycle.Id.Value} from DB. Status: {_cycle.Status}, Position: {_cycle.PositionQuantity.Value}");
                    
                    _logger.Info($"Found active order {activeOrder.Id}. Simulating reconciliation...");
                    var orderId = new InternalOrderId(activeOrder.Comment);
                    _adapter.Reconcile(new[] { orderId });
                }
            }

            if (loadedCycle == null)
            {
                // Dispatch StartNewCycleMessage to background thread to avoid race conditions with synchronous RejectionMessage
                _logger.Info("Dispatching initial StartNewCycleMessage to EngineLoop...");
                _engineLoop.Enqueue(new StartNewCycleMessage());
            }

            _throttleTimer = new System.Threading.Timer(_ => 
            {
                if (_engineLoop != null)
                    _engineLoop.Enqueue(new TickMessage(DateTime.UtcNow));
            }, null, 300, 300);
        }

        private void ProcessStartNewCycle()
        {
            _logger.Info($"Generating new TradeCycle...");
            
            var rules = new InstrumentRules(
                "USDT",
                (decimal)TestSymbol.TickSize,
                (decimal)TestSymbol.MinLot,
                10m // MinNotional
            );

            var settings = new DcaSettings(
                TpPercent: (decimal)TpPercent,
                StepPercent: (decimal)StepPercent,
                VolumeScale: (decimal)VolumeScale,
                MaxGridLevels: MaxGridLevels,
                ActiveGridWindow: ActiveGridWindow,
                LastLevelStopPercent: (decimal)LastLevelStopPercent
            );

            _cycle = new TradeCycle(TradeCycleId.New(), settings, rules, OrderSide.Buy);
            
            var currentPrice = TestSymbol.Ask;
            if (double.IsNaN(currentPrice) || currentPrice <= 0)
                currentPrice = TestSymbol.Last;

            if (double.IsNaN(currentPrice) || currentPrice <= 0)
            {
                _logger.Error("Could not determine current price (Ask/Last is NaN or 0). Please check symbol connection.");
                return;
            }

            var entryPrice = new DCAnt2.Core.Domain.Price((decimal)currentPrice);
            var entryQty = new Quantity((decimal)TestSymbol.MinLot);
        
            _logger.Info($"Starting TradeCycle {_cycle.Id.Value}: Entry Price {entryPrice.Value}, Qty {entryQty.Value}");
            _cycle.Start(entryPrice, entryQty);
            // The messageHandler will automatically process the outbox since this is called within the handler
        }
        
        private void OnOrdersHistoryAdded(OrderHistory order)
        {
            if (_adapter != null && order != null)
                _adapter.HandleOrderHistoryAdded(order);
        }

        protected override void OnStop()
        {
            _logger?.Info("Stopping Live Test...");
            
            _throttleTimer?.Dispose();
            _restartTimer?.Dispose();
            
            TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded -= OnOrdersHistoryAdded;
            if (_engineLoop != null)
            {
                _engineLoop.StopAsync().Wait();
                _engineLoop.Dispose();
            }
            
            _logger?.Dispose();
        }
    }
}
