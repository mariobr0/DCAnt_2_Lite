using System;
using System.IO;
using System.Linq;
using System.Threading;
using TradingPlatform.BusinessLayer;

namespace DCAnt2.Plugin.Diagnostics;

public enum ContractTestScenario
{
    ObserveOnly,
    PlaceAndObserve,
    CancelAndObserve
}

public class SingleOrderContractStrategy : Strategy
{
    [InputParameter("Account", 1)]
    public Account TestAccount = null!;

    [InputParameter("Symbol", 2)]
    public Symbol TestSymbol = null!;

    [InputParameter("Scenario", 10)]
    public ContractTestScenario Scenario = ContractTestScenario.ObserveOnly;

    [InputParameter("Target Marker (for Observe/Cancel)", 20)]
    public string InputTargetMarker = "";

    [InputParameter("Observation Timeout (seconds)", 30)]
    public int ObservationTimeoutSeconds = 30;

    [InputParameter("Test Price (PlaceAndObserve)", 40)]
    public double TestPrice = 0.0;

    [InputParameter("Test Quantity", 50)]
    public double TestQuantity = 1.0;

    [InputParameter("Test Side", 60)]
    public Side TestSide = Side.Buy;

    private string _runId = "";
    private string _targetMarker = "";
    private string _logPath = "";
    private readonly object _logLock = new();

    private volatile bool _observationFinished;
    private volatile bool _stopping;
    private Timer? _timer;

    public SingleOrderContractStrategy()
    {
        Name = $"DCAnt2 Single Order Contract {DateTime.Now:HHmmss}";
        Description = "Diagnostic strategy for observing Quantower Place/Cancel contracts";
    }

    protected override void OnCreated()
    {
        base.OnCreated();
    }

    protected override void OnRun()
    {
        try
        {
            ValidateCommonInputs();
            GenerateRunId();
            _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"DCAnt2_Contracts_{_runId}.log");

            if (Scenario == ContractTestScenario.PlaceAndObserve)
            {
                GenerateTargetMarker();
                WriteLog($"TARGET_MARKER_CREATED={_targetMarker}");
            }
            else
            {
                _targetMarker = InputTargetMarker;
                if (string.IsNullOrWhiteSpace(_targetMarker))
                {
                    throw new InvalidOperationException("TargetMarker is required for ObserveOnly or CancelAndObserve");
                }
            }

            WriteSnapshot("Start");

            // Подписка на фактически доступные события Core.Instance
            global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded += OnOrderEvent;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved += OnOrderEvent;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded += OnOrderHistoryEvent;
            
            global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded += OnPositionEvent;
            global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved += OnPositionEvent;

            _timer = new Timer(OnObservationTimeout, null, TimeSpan.FromSeconds(ObservationTimeoutSeconds), Timeout.InfiniteTimeSpan);

            switch (Scenario)
            {
                case ContractTestScenario.ObserveOnly:
                    // Только наблюдать, ничего не отправлять
                    break;
                case ContractTestScenario.PlaceAndObserve:
                    ExecutePlaceScenario();
                    break;
                case ContractTestScenario.CancelAndObserve:
                    ExecuteCancelScenario();
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteLog($"ERROR: {ex.Message}");
            Stop();
        }
    }

    protected override void OnStop()
    {
        // 1. _stopping = true
        _stopping = true;
        
        // 2. остановить и Dispose timer
        _timer?.Dispose();
        _timer = null;

        // 3. отписаться от событий
        global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded -= OnOrderEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved -= OnOrderEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded -= OnOrderHistoryEvent;

        global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded -= OnPositionEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved -= OnPositionEvent;

        // 4. собрать финальный snapshot
        var finalSnapshot = BuildSnapshotLine("OnStop");

        // 5. записать финальный snapshot
        WriteLogLine(finalSnapshot);

        // 6. записать итог cleanup
        var activeOrders = FindActiveTestOrders();
        var position = ReadPosition();
        
        bool needsCleanup = activeOrders.Length > 0 || (position != null && Math.Abs(position.Quantity) > 0);
        WriteLogLine($"Manual cleanup required: {(needsCleanup ? "Yes" : "No")}");

        // 7. завершить логирование - (поскольку мы используем File.AppendAllText, больше ничего закрывать не нужно)
    }

    private void OnObservationTimeout(object? state)
    {
        if (_observationFinished)
        {
            return; // Защита от ошибочного двойного вызова
        }
        _observationFinished = true;

        var snapshot = BuildSnapshotLine("Timeout");
        WriteLogLine(snapshot);
        WriteLogLine("Observation timeout reached");
    }

    private void OnOrderEvent(Order order)
    {
        if (_stopping) return;
        if (!IsMatchingOrder(order)) return;

        var eventName = "OrderEvent";
        if (_observationFinished) eventName = "AFTER_OBSERVATION_TIMEOUT_" + eventName;

        var line = BuildSnapshotLine(eventName, order: order);
        WriteLogLine(line);
    }

    private void OnOrderHistoryEvent(OrderHistory orderHistory)
    {
        if (_stopping) return;
        // Map OrderHistory properties back to our snapshot log format as best as possible
        // Actually OrderHistory doesn't match Order exactly, so we do a specialized snapshot line
        if (orderHistory.Account.Id != TestAccount?.Id || orderHistory.Symbol.Id != TestSymbol?.Id) return;
        
        var eventName = "OrderHistoryEvent";
        if (_observationFinished) eventName = "AFTER_OBSERVATION_TIMEOUT_" + eventName;

        var line = BuildSnapshotLine(eventName, orderHistory: orderHistory);
        WriteLogLine(line);
    }

    private void OnPositionEvent(Position position)
    {
        if (_stopping) return;
        if (position.Account.Id != TestAccount?.Id || position.Symbol.Id != TestSymbol?.Id) return;

        var eventName = "PositionEvent";
        if (_observationFinished) eventName = "AFTER_OBSERVATION_TIMEOUT_" + eventName;

        var line = BuildSnapshotLine(eventName, position: position);
        WriteLogLine(line);
    }

    private void ExecutePlaceScenario()
    {
        var position = ReadPosition();
        if (position != null && Math.Abs(position.Quantity) > 0)
        {
            throw new InvalidOperationException($"Place forbidden. Position is not zero. Found: {position.Quantity}");
        }
        if (position == null)
        {
            WriteLog("WARNING: Position could not be read definitively. Assuming 0, but this is dangerous.");
            // According to plan, if we cannot definitively know it's zero, we might forbid it. 
            // In Quantower, `Core.Positions.FirstOrDefault` returning null means no position exists.
            // So position == null actually MEANS strictly 0 position.
        }
        
        var activeOrders = FindActiveTestOrders();
        if (activeOrders.Length > 0)
        {
            throw new InvalidOperationException($"Place forbidden. Found {activeOrders.Length} active qtct_ orders");
        }

        WriteSnapshot("Before API Call");
        
        var request = new PlaceOrderRequestParameters
        {
            Account = TestAccount,
            Symbol = TestSymbol,
            OrderTypeId = OrderType.Limit,
            Side = TestSide,
            Quantity = TestQuantity,
            Price = TestPrice,
            TimeInForce = TimeInForce.GTC,
            Comment = _targetMarker
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = global::TradingPlatform.BusinessLayer.Core.Instance.PlaceOrder(request);
        watch.Stop();

        WriteLog($"PlaceOrder API result: Status={result.Status}, Message={result.Message}, ReturnedOrderId={result.OrderId}, ElapsedMs={watch.ElapsedMilliseconds}");
        WriteSnapshot("After API Call");
    }

    private void ExecuteCancelScenario()
    {
        var matches = FindMatchingActiveOrders();
        if (matches.Length == 0)
        {
            WriteLog("Cancel skipped. Found 0 matching orders.");
            return;
        }
        if (matches.Length > 1)
        {
            WriteLog($"Cancel skipped. Found {matches.Length} matching orders.");
            return;
        }

        var targetOrder = matches[0];
        WriteSnapshot("Before API Call", targetOrder);

        var request = new CancelOrderRequestParameters
        {
            OrderId = targetOrder.Id
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = global::TradingPlatform.BusinessLayer.Core.Instance.CancelOrder(request);
        watch.Stop();

        WriteLog($"CancelOrder API result: Status={result.Status}, Message={result.Message}, ElapsedMs={watch.ElapsedMilliseconds}");
        WriteSnapshot("After API Call", targetOrder);
    }

    private void ValidateCommonInputs()
    {
        if (TestAccount == null || TestSymbol == null)
            throw new InvalidOperationException("TestAccount and TestSymbol must be selected.");
    }

    private Order[] FindActiveTestOrders()
    {
        return global::TradingPlatform.BusinessLayer.Core.Instance.Orders.Where(o =>
            o.Account.Id == TestAccount?.Id &&
            o.Symbol.Id == TestSymbol?.Id &&
            o.Status == OrderStatus.Opened &&
            (o.Comment?.StartsWith("qtct_") ?? false)
        ).ToArray();
    }

    private Order[] FindMatchingActiveOrders()
    {
        return global::TradingPlatform.BusinessLayer.Core.Instance.Orders.Where(o =>
            o.Account.Id == TestAccount?.Id &&
            o.Symbol.Id == TestSymbol?.Id &&
            o.Status == OrderStatus.Opened &&
            o.Comment == _targetMarker
        ).ToArray();
    }

    private Position? ReadPosition()
    {
        return global::TradingPlatform.BusinessLayer.Core.Instance.Positions.FirstOrDefault(p =>
            p.Account.Id == TestAccount?.Id &&
            p.Symbol.Id == TestSymbol?.Id);
    }

    private bool IsMatchingOrder(Order order)
    {
        return order.Account.Id == TestAccount?.Id &&
               order.Symbol.Id == TestSymbol?.Id &&
               order.Comment == _targetMarker;
    }

    private void GenerateRunId()
    {
        _runId = $"run_{DateTime.UtcNow:yyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
    }

    private void GenerateTargetMarker()
    {
        _targetMarker = $"qtct_{DateTime.UtcNow:ddMMyy}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }

    private void WriteSnapshot(string eventName, Order? order = null)
    {
        var line = BuildSnapshotLine(eventName, order);
        WriteLogLine(line);
    }

    private string BuildSnapshotLine(string eventName, Order? order = null, Position? position = null, OrderHistory? orderHistory = null)
    {
        position ??= ReadPosition();

        var ts = DateTime.UtcNow.ToString("O");
        var pQty = position != null ? position.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";
        var pPrice = position != null ? position.OpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";

        var oId = order != null ? order.Id : (orderHistory != null ? orderHistory.Id : "Unknown");
        var oStatus = order != null ? order.Status.ToString() : (orderHistory != null ? orderHistory.Status.ToString() : "Unknown");
        var oComment = order != null ? order.Comment : (orderHistory != null ? orderHistory.Comment : "Unknown");
        var oPrice = order != null ? order.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oTQty = order != null ? order.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oFQty = order != null ? order.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oAvgP = order != null ? order.AverageFillPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.AverageFillPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");

        var threadId = Thread.CurrentThread.ManagedThreadId;

        return $"[{ts}] [{_runId}] [{_targetMarker}] [{Scenario}] [{eventName}] " +
               $"AccountId={TestAccount?.Id} SymbolId={TestSymbol?.Id} " +
               $"OrderId={oId} Status={oStatus} Comment={oComment} " +
               $"Price={oPrice} TotalQty={oTQty} FilledQty={oFQty} AverageFillPrice={oAvgP} " +
               $"PositionQty={pQty} PositionOpenPrice={pPrice} ThreadId={threadId}";
    }

    private void WriteLog(string message)
    {
        var ts = DateTime.UtcNow.ToString("O");
        WriteLogLine($"[{ts}] [{_runId}] [{_targetMarker}] [{Scenario}] {message}");
    }

    private void WriteLogLine(string line)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        lock (_logLock)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }
}
