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

    [InputParameter("Scenario (ObserveOnly, PlaceAndObserve, CancelAndObserve)", 10)]
    public string Scenario = "ObserveOnly";

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
    private string? _observedOrderId;
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
            
            var logsDir = @"C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\logs";
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
            _logPath = Path.Combine(logsDir, $"DCAnt2_Contracts_{_runId}.log");

            if (Scenario == "PlaceAndObserve")
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

            WriteLogLine(BuildSnapshotLine("Strategy", "StartSnapshot"));

            // Подписка на фактически доступные события Core.Instance
            global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded += OnOrderAdded;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved += OnOrderRemoved;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded += OnOrderHistoryEvent;
            
            global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded += OnPositionAdded;
            global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved += OnPositionRemoved;

            _timer = new Timer(OnObservationTimeout, null, TimeSpan.FromSeconds(ObservationTimeoutSeconds), Timeout.InfiniteTimeSpan);

            switch (Scenario)
            {
                case "ObserveOnly":
                    // Только наблюдать, ничего не отправлять
                    break;
                case "PlaceAndObserve":
                    ExecutePlaceScenario();
                    break;
                case "CancelAndObserve":
                    ExecuteCancelScenario();
                    break;
                default:
                    WriteLog($"Unknown Scenario: {Scenario}. Defaulting to ObserveOnly.");
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteLogLine($"[{DateTime.UtcNow:O}] [{_runId}] [{_targetMarker}] [{Scenario}] Error={Quote(ex.ToString())}");
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
        global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded -= OnOrderAdded;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved -= OnOrderRemoved;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded -= OnOrderHistoryEvent;

        global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded -= OnPositionAdded;
        global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved -= OnPositionRemoved;

        // 4. собрать итог cleanup
        var activeTestOrdersCount = FindActiveTestOrders().Length;
        var position = ReadPosition();
        
        string cleanupRequired = "Unknown";
        string cleanupReason = "Unknown";
        
        if (activeTestOrdersCount > 0) 
        {
            cleanupRequired = "Yes";
            cleanupReason = "ActiveTestOrdersFound";
        }
        else if (position != null && Math.Abs(position.Quantity) > 0)
        {
            cleanupRequired = "Yes";
            cleanupReason = "OpenPositionFound";
        }
        else if (activeTestOrdersCount == 0 && (position == null || Math.Abs(position.Quantity) == 0))
        {
            cleanupRequired = "No";
            cleanupReason = "NoTestOrdersAndNoPosition";
        }

        var extra = $"ManualCleanupRequired={cleanupRequired} CleanupReason={cleanupReason}";
        var finalSnapshot = BuildSnapshotLine("Strategy", "OnStop", extraInfo: extra);

        // 5. записать финальный snapshot
        WriteLogLine(finalSnapshot);
    }

    private void OnObservationTimeout(object? state)
    {
        if (_observationFinished)
        {
            return; // Защита от ошибочного двойного вызова
        }
        _observationFinished = true;

        var snapshot = BuildSnapshotLine("Timer", "ObservationTimeout", extraInfo: "Message=\"Observation timeout reached\"");
        WriteLogLine(snapshot);
    }

    private void OnOrderAdded(Order order) => HandleOrderEvent("OrderAdded", order);
    private void OnOrderRemoved(Order order) => HandleOrderEvent("OrderRemoved", order);

    private void HandleOrderEvent(string source, Order order)
    {
        if (_stopping) return;
        if (!IsMatchingOrder(order)) return;
        
        _observedOrderId = order.Id;
        
        var line = BuildSnapshotLine(source, "OrderState", order: order);
        WriteLogLine(line);
    }

    private void OnOrderHistoryEvent(OrderHistory orderHistory)
    {
        if (_stopping) return;
        if (orderHistory.Account.Id != TestAccount?.Id || orderHistory.Symbol.Id != TestSymbol?.Id) return;
        
        bool markerMatches = orderHistory.Comment == _targetMarker;
        bool orderIdMatches = !string.IsNullOrWhiteSpace(_observedOrderId) && orderHistory.Id == _observedOrderId;

        if (!markerMatches && !orderIdMatches) return;
        
        string extra = $"MarkerMatches={markerMatches} OrderIdMatches={orderIdMatches}";
        var line = BuildSnapshotLine("OrdersHistoryAdded", "OrderHistoryState", orderHistory: orderHistory, extraInfo: extra);
        WriteLogLine(line);
    }

    private void OnPositionAdded(Position position) => HandlePositionEvent("PositionAdded", position);
    private void OnPositionRemoved(Position position) => HandlePositionEvent("PositionRemoved", position);

    private void HandlePositionEvent(string source, Position position)
    {
        if (_stopping) return;
        if (position.Account.Id != TestAccount?.Id || position.Symbol.Id != TestSymbol?.Id) return;
        var line = BuildSnapshotLine(source, "PositionState", position: position);
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
        }
        
        var activeOrders = FindActiveTestOrders();
        if (activeOrders.Length > 0)
        {
            throw new InvalidOperationException($"Place forbidden. Found {activeOrders.Length} active qtct_ orders");
        }

        WriteLogLine(BuildSnapshotLine("Strategy", "BeforeApiCall"));
        
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

        WriteLog($"PlaceOrder API result: Status={result.Status}, Message={Quote(result.Message)}, ReturnedOrderId={Quote(result.OrderId)}, ElapsedMs={watch.ElapsedMilliseconds}");
        WriteLogLine(BuildSnapshotLine("Strategy", "AfterApiCall"));
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
        WriteLogLine(BuildSnapshotLine("Strategy", "BeforeApiCall", order: targetOrder));

        var request = new CancelOrderRequestParameters
        {
            OrderId = targetOrder.Id
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = global::TradingPlatform.BusinessLayer.Core.Instance.CancelOrder(request);
        watch.Stop();

        WriteLog($"CancelOrder API result: Status={result.Status}, Message={Quote(result.Message)}, ElapsedMs={watch.ElapsedMilliseconds}");
        WriteLogLine(BuildSnapshotLine("Strategy", "AfterApiCall", order: targetOrder));
    }

    private void ValidateCommonInputs()
    {
        if (TestAccount == null || TestSymbol == null)
            throw new InvalidOperationException("TestAccount and TestSymbol must be selected.");
    }

    private int CountActiveOrders()
    {
        return global::TradingPlatform.BusinessLayer.Core.Instance.Orders.Count(o =>
            o.Account.Id == TestAccount?.Id &&
            o.Symbol.Id == TestSymbol?.Id &&
            o.Status == OrderStatus.Opened);
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

    private static string Quote(string? value)
    {
        if (value is null)
        {
            return "Unknown";
        }
        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return $"\"{escaped}\"";
    }

    private string BuildSnapshotLine(string source, string eventName, Order? order = null, Position? position = null, OrderHistory? orderHistory = null, string extraInfo = "")
    {
        position ??= ReadPosition();

        var ts = DateTime.UtcNow.ToString("O");
        
        string positionState = "None";
        string pQty = "0";
        string pPrice = "Unknown";
        if (position != null)
        {
            if (Math.Abs(position.Quantity) > 0)
            {
                positionState = "Open";
                pQty = position.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
                pPrice = position.OpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                positionState = "None";
                pQty = "0";
            }
        }
        else
        {
            // FirstOrDefault returns null if there is no open position for this account/symbol
            positionState = "None";
            pQty = "0";
        }

        var activeOrdersCount = CountActiveOrders();
        var activeTestOrdersCount = FindActiveTestOrders().Length;
        var matchingOrdersCount = FindMatchingActiveOrders().Length;

        var oId = order != null ? order.Id : (orderHistory != null ? orderHistory.Id : "Unknown");
        var oStatus = order != null ? order.Status.ToString() : (orderHistory != null ? orderHistory.Status.ToString() : "Unknown");
        var oComment = order != null ? order.Comment : (orderHistory != null ? orderHistory.Comment : "Unknown");
        var oPrice = order != null ? order.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oTQty = order != null ? order.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oFQty = order != null ? order.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oAvgP = order != null ? order.AverageFillPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.AverageFillPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");

        var threadId = Thread.CurrentThread.ManagedThreadId;
        var accName = TestAccount != null ? Quote(TestAccount.Name) : "Unknown";
        var obsFin = _observationFinished ? " ObservationFinished=True" : "";

        return $"[{ts}] [{_runId}] [{_targetMarker}] [{Scenario}] [{source}] [{eventName}] " +
               $"AccountId={Quote(TestAccount?.Id)} AccountName={accName} SymbolId={Quote(TestSymbol?.Id)} " +
               $"ActiveOrdersCount={activeOrdersCount} ActiveTestOrdersCount={activeTestOrdersCount} MatchingOrdersCount={matchingOrdersCount} " +
               $"PositionState={positionState} PositionQty={pQty} PositionOpenPrice={pPrice} " +
               $"OrderId={Quote(oId)} Status={Quote(oStatus)} Comment={Quote(oComment)} " +
               $"Price={oPrice} TotalQty={oTQty} FilledQty={oFQty} AverageFillPrice={oAvgP}{obsFin}" +
               (string.IsNullOrEmpty(extraInfo) ? "" : $" {extraInfo}") +
               $" ThreadId={threadId}";
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
