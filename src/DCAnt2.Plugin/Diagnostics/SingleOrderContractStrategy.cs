using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DCAnt2.Plugin.Diagnostics;

public class SingleOrderContractStrategy : Strategy
{
    [InputParameter("Account", 1)]
    public Account TestAccount = null!;

    [InputParameter("Symbol", 2)]
    public Symbol TestSymbol = null!;

    [InputParameter("Scenario (ObserveFill, PlaceForFullFill, PlaceForPartialFill, CancelAndObserve)", 10)]
    public string Scenario = "ObserveFill";

    [InputParameter("Target Marker (for Observe/Cancel)", 20)]
    public string InputTargetMarker = "";

    [InputParameter("Observation Timeout (seconds)", 30)]
    public int ObservationTimeoutSeconds = 30;

    [InputParameter("Test Price (Place...)", 40, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double TestPrice = 0.0;

    [InputParameter("Test Quantity", 50, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double TestQuantity = 1.0;

    [InputParameter("Test Side", 60)]
    public Side TestSide = Side.Buy;

    private string _runId = "";
    private string _targetMarker = "";
    private string _logPath = "";
    private string? _observedOrderId;
    private readonly object _logLock = new object();
    private readonly object _fillStateLock = new object();
    private readonly object _eventProcessingLock = new object();

    private volatile bool _observationFinished;
    private volatile bool _stopping;
    private Timer? _timer;

    private long _logSequence = 0;
    private readonly Dictionary<string, double> _previousFilledQuantities = new();

    public SingleOrderContractStrategy()
    {
        Name = $"DCAnt2 Single Order Contract {DateTime.Now:HHmmss}";
        Description = "Diagnostic strategy for observing Quantower Place/Cancel contracts";
    }

    protected override void OnRun()
    {
        try
        {
            _stopping = false;
            _observationFinished = false;
            _observedOrderId = null;
            _logSequence = 0;
            _previousFilledQuantities.Clear();

            ValidateCommonInputs();
            GenerateRunId();

            var logsDir = @"C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\logs";
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
            _logPath = Path.Combine(logsDir, $"DCAnt2_Contracts_{_runId}.log");

            if (Scenario == "PlaceForFullFill" || Scenario == "PlaceForPartialFill")
            {
                GenerateTargetMarker();
                WriteLog("System", "TARGET_MARKER_CREATED", extraInfo: $"TargetMarker={Quote(_targetMarker)}");
            }
            else
            {
                _targetMarker = InputTargetMarker;
            }

            WriteLogLine(BuildSnapshotLine("Strategy", "StartSnapshot"));

            global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded += OnOrderAdded;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved += OnOrderRemoved;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded += OnOrderHistoryEvent;

            global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded += OnPositionAdded;
            global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved += OnPositionRemoved;

            // Public API check for Execution/Trade
            // Reflection is strictly forbidden by AGENTS.md rules. If it's not exposed publicly, it's NotSupported.
            WriteLog("System", "TradeExecutionApi", extraInfo: "SupportStatus=NotSupported Reason=\"ExecutionAdded and TradeAdded are not publicly exposed by TradingPlatform.BusinessLayer.Core in this API version\"");

            _timer = new Timer(OnObservationTimeout, null, TimeSpan.FromSeconds(ObservationTimeoutSeconds), Timeout.InfiniteTimeSpan);

            switch (Scenario)
            {
                case "ObserveFill":
                    break;
                case "PlaceForFullFill":
                case "PlaceForPartialFill":
                    ExecutePlaceScenario();
                    break;
                case "CancelAndObserve":
                    ExecuteCancelScenario();
                    break;
                default:
                    WriteLog("Strategy", "UnknownScenario", extraInfo: $"Message=\"Unknown Scenario: {Scenario}. Defaulting to ObserveFill.\"");
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteLog("Strategy", "Error", extraInfo: $"Exception={Quote(ex.ToString())}");
            Stop();
        }
    }

    protected override void OnStop()
    {
        _stopping = true;
        _timer?.Dispose();
        _timer = null;

        global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded -= OnOrderAdded;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved -= OnOrderRemoved;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded -= OnOrderHistoryEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded -= OnPositionAdded;
        global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved -= OnPositionRemoved;

        var activeTestOrdersCount = FindActiveTestOrders().Length;
        var position = ReadPosition(out string positionState);

        string cleanupRequired = "Unknown";
        string cleanupReason = "Unknown";

        if (activeTestOrdersCount > 0)
        {
            cleanupRequired = "Yes";
            cleanupReason = "ActiveTestOrdersFound";
        }
        else if (positionState == "Open")
        {
            cleanupRequired = "Yes";
            cleanupReason = "OpenPositionFound";
        }
        else if (activeTestOrdersCount == 0 && positionState == "None")
        {
            cleanupRequired = "No";
            cleanupReason = "NoTestOrdersAndNoPosition";
        }

        var extra = $"ManualCleanupRequired={cleanupRequired} CleanupReason={cleanupReason}";
        var finalSnapshot = BuildSnapshotLine("Strategy", "OnStop", extraInfo: extra);

        WriteLogLine(finalSnapshot);
    }

    private void OnObservationTimeout(object? state)
    {
        if (_observationFinished) return;
        _observationFinished = true;

        var snapshot = BuildSnapshotLine("Timer", "ObservationTimeout", extraInfo: "Message=\"Observation timeout reached\"");
        WriteLogLine(snapshot);
    }

    private void OnOrderAdded(Order order) => HandleOrderEvent("OrderAdded", order);
    private void OnOrderRemoved(Order order) => HandleOrderEvent("OrderRemoved", order);

    private void HandleOrderEvent(string source, Order order)
    {
        bool triggeredProbes = false;
        double filledQty = 0;

        lock (_eventProcessingLock)
        {
            if (_stopping) return;

            bool accMatch = order.Account?.Id == TestAccount?.Id;
            bool symMatch = order.Symbol?.Id == TestSymbol?.Id;
            bool markerMatches = !string.IsNullOrEmpty(_targetMarker) && order.Comment == _targetMarker;
            bool orderIdMatches = !string.IsNullOrWhiteSpace(_observedOrderId) && order.Id == _observedOrderId;

            if (markerMatches && string.IsNullOrWhiteSpace(_observedOrderId))
            {
                _observedOrderId = order.Id;
                orderIdMatches = true;
            }

            if (!accMatch || !symMatch || (!markerMatches && !orderIdMatches)) return;

            ProcessFilledQuantity(order.Id, order.FilledQuantity, out string prevQtyStr, out string diffStr, out triggeredProbes);
            filledQty = order.FilledQuantity;

            string timeStr = "Unknown";
            string extra = $"AccMatch={accMatch} SymMatch={symMatch} MarkerMatches={markerMatches} OrderIdMatches={orderIdMatches} " +
                           $"PreviousFilledQty={prevQtyStr} CurrentFilledQty={order.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture)} ObservedDifference={diffStr}";

            var line = BuildSnapshotLine(source, "OrderState", order: order, providerTime: timeStr, extraInfo: extra);
            WriteLogLine(line);
        }

        if (triggeredProbes)
        {
            TriggerProbes(filledQty);
        }
    }

    private void OnOrderHistoryEvent(OrderHistory orderHistory)
    {
        bool triggeredProbes = false;
        double filledQty = 0;

        lock (_eventProcessingLock)
        {
            if (_stopping) return;

            bool accMatch = orderHistory.Account?.Id == TestAccount?.Id;
            bool symMatch = orderHistory.Symbol?.Id == TestSymbol?.Id;
            bool markerMatches = !string.IsNullOrEmpty(_targetMarker) && orderHistory.Comment == _targetMarker;
            bool orderIdMatches = !string.IsNullOrWhiteSpace(_observedOrderId) && orderHistory.Id == _observedOrderId;

            if (markerMatches && string.IsNullOrWhiteSpace(_observedOrderId))
            {
                _observedOrderId = orderHistory.Id;
                orderIdMatches = true;
            }

            if (!accMatch || !symMatch || (!markerMatches && !orderIdMatches)) return;

            ProcessFilledQuantity(orderHistory.Id, orderHistory.FilledQuantity, out string prevQtyStr, out string diffStr, out triggeredProbes);
            filledQty = orderHistory.FilledQuantity;

            string timeStr = "Unknown";
            string extra = $"AccMatch={accMatch} SymMatch={symMatch} MarkerMatches={markerMatches} OrderIdMatches={orderIdMatches} " +
                           $"PreviousFilledQty={prevQtyStr} CurrentFilledQty={orderHistory.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture)} ObservedDifference={diffStr}";
            var line = BuildSnapshotLine("OrdersHistoryAdded", "OrderHistoryState", orderHistory: orderHistory, providerTime: timeStr, extraInfo: extra);
            WriteLogLine(line);
        }

        if (triggeredProbes)
        {
            TriggerProbes(filledQty);
        }
    }

    private void ProcessFilledQuantity(string orderId, double currentFilled, out string prevQtyStr, out string diffStr, out bool triggeredProbes)
    {
        triggeredProbes = false;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            prevQtyStr = "Unknown";
            diffStr = "Unknown";
            return;
        }

        lock (_fillStateLock)
        {
            if (_previousFilledQuantities.TryGetValue(orderId, out double previous))
            {
                prevQtyStr = previous.ToString(System.Globalization.CultureInfo.InvariantCulture);
                double diff = currentFilled - previous;
                diffStr = diff.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (currentFilled > 0 && Math.Abs(diff) > 1e-8)
                {
                    triggeredProbes = true;
                }
            }
            else
            {
                prevQtyStr = "Unknown";
                diffStr = "Unknown";
                if (currentFilled > 0)
                {
                    triggeredProbes = true;
                }
            }

            _previousFilledQuantities[orderId] = currentFilled;
        }
    }

    private void TriggerProbes(double triggerFilledQty)
    {
        // triggerFilledQty is captured locally before passing to async probe delays
        ScheduleProbe(100, triggerFilledQty);
        ScheduleProbe(250, triggerFilledQty);
        ScheduleProbe(500, triggerFilledQty);
        ScheduleProbe(1000, triggerFilledQty);
    }

    private void OnPositionAdded(Position position) => HandlePositionEvent("PositionAdded", position);
    private void OnPositionRemoved(Position position) => HandlePositionEvent("PositionRemoved", position);

    private void HandlePositionEvent(string source, Position position)
    {
        lock (_eventProcessingLock)
        {
            if (_stopping) return;
            if (position.Account.Id != TestAccount?.Id || position.Symbol.Id != TestSymbol?.Id) return;
            var line = BuildSnapshotLine(source, "PositionState", position: position);
            WriteLogLine(line);
        }
    }

    private void ExecutePlaceScenario()
    {
        if (TestAccount == null || TestSymbol == null || TestPrice <= 0 || TestQuantity <= 0)
        {
            throw new InvalidOperationException("Place forbidden. Account, Symbol must be set and Price/Quantity must be > 0.");
        }

        var position = ReadPosition(out string positionState);
        if (positionState == "Unknown")
        {
            WriteLog("Strategy", "POSITION_CHECK", extraInfo: $"Result=Unknown PlaceAllowed=False");
            throw new InvalidOperationException("Place forbidden. Position state is Unknown.");
        }
        if (positionState == "Open")
        {
            WriteLog("Strategy", "POSITION_CHECK", extraInfo: $"Result=Open PositionQty={position?.Quantity} PlaceAllowed=False");
            throw new InvalidOperationException($"Place forbidden. Position is not zero. Found: {position?.Quantity}");
        }

        WriteLog("Strategy", "POSITION_CHECK", extraInfo: "Result=None PositionQty=0 PlaceAllowed=True");

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

        if (result.Status.ToString() == "Success" && !string.IsNullOrWhiteSpace(result.OrderId))
        {
            _observedOrderId = result.OrderId;
        }

        WriteLog("Strategy", "AfterApiCall", extraInfo: $"PlaceOrder API result: Status={result.Status} Message={Quote(result.Message)} ReturnedOrderId={Quote(result.OrderId)} ElapsedMs={watch.ElapsedMilliseconds}");
    }

    private void ScheduleProbe(int delayMs, double triggerFilledQty)
    {
        Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            if (_stopping) return;
            try
            {
                var pos = ReadPosition(out string pState);
                string pQty = pos != null ? pos.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
                string pPrice = pos != null ? pos.OpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";

                string extra = $"TriggerFilledQty={triggerFilledQty.ToString(System.Globalization.CultureInfo.InvariantCulture)} ProbeDelayMs={delayMs} PositionQty={pQty} PositionOpenPrice={pPrice}";
                WriteLogLine(BuildSnapshotLine("Probe", "PositionProbe", position: pos, positionState: pState, extraInfo: extra));
            }
            catch (Exception ex)
            {
                if (!_stopping) WriteLog("Probe", "Error", extraInfo: $"Probe {delayMs}ms failed: {Quote(ex.Message)}");
            }
        });
    }

    private void ExecuteCancelScenario()
    {
        var matches = FindMatchingActiveOrders();
        if (matches.Length == 0)
        {
            WriteLog("Strategy", "CancelSkipped", extraInfo: "Reason=\"Found 0 matching orders.\"");
            return;
        }
        if (matches.Length > 1)
        {
            WriteLog("Strategy", "CancelSkipped", extraInfo: $"Reason=\"Found {matches.Length} matching orders.\"");
            return;
        }

        var targetOrder = matches[0];
        _observedOrderId = targetOrder.Id;
        WriteLogLine(BuildSnapshotLine("Strategy", "BeforeApiCall", order: targetOrder));

        bool accountPresent = targetOrder.Account != null;
        bool connectionPresent = !string.IsNullOrEmpty(targetOrder.ConnectionId);

        WriteLog("Strategy", "CANCEL_REQUEST", extraInfo: $"OrderObjectPresent=True OrderId=\"{targetOrder.Id}\" AccountPresent={accountPresent} AccountId=\"{targetOrder.Account?.Id ?? "null"}\" ConnectionId=\"{targetOrder.ConnectionId ?? "null"}\" SymbolId=\"{targetOrder.Symbol?.Id ?? "null"}\" TargetMarker=\"{targetOrder.Comment ?? "null"}\" CancelAllowed={(accountPresent && connectionPresent)}");

        if (!accountPresent || !connectionPresent)
        {
            WriteLog("Strategy", "CancelSkipped", extraInfo: "Reason=\"MissingConnectionContext\"");
            return;
        }

        var request = new CancelOrderRequestParameters()
        {
            Order = targetOrder
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = global::TradingPlatform.BusinessLayer.Core.Instance.CancelOrder(request);
            watch.Stop();
            WriteLog("Strategy", "CancelApiResult", extraInfo: $"Status={result.Status} Message={Quote(result.Message)} ElapsedMs={watch.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            watch.Stop();
            WriteLog("Strategy", "CancelApiException", extraInfo: $"ElapsedMs={watch.ElapsedMilliseconds} Exception={ex.GetType().Name}: {Quote(ex.Message)}");
        }

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
        if (string.IsNullOrEmpty(_targetMarker)) return Array.Empty<Order>();
        return global::TradingPlatform.BusinessLayer.Core.Instance.Orders.Where(o =>
            o.Account.Id == TestAccount?.Id &&
            o.Symbol.Id == TestSymbol?.Id &&
            o.Status == OrderStatus.Opened &&
            o.Comment == _targetMarker
        ).ToArray();
    }

    private Position? ReadPosition(out string positionState)
    {
        try
        {
            var pos = global::TradingPlatform.BusinessLayer.Core.Instance.Positions.FirstOrDefault(p =>
                p.Account.Id == TestAccount?.Id &&
                p.Symbol.Id == TestSymbol?.Id);

            if (pos == null)
            {
                positionState = "None";
                return null;
            }

            if (Math.Abs(pos.Quantity) > 0)
            {
                positionState = "Open";
                return pos;
            }

            positionState = "None";
            return pos;
        }
        catch
        {
            positionState = "Unknown";
            return null;
        }
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

    private string BuildSnapshotLine(string source, string eventName, Order? order = null, Position? position = null, string? positionState = null, OrderHistory? orderHistory = null, string providerTime = "Unknown", string extraInfo = "")
    {
        if (string.IsNullOrWhiteSpace(positionState))
        {
            if (position != null)
            {
                positionState = Math.Abs(position.Quantity) > 0 ? "Open" : "None";
            }
            else
            {
                position = ReadPosition(out positionState);
            }
        }

        if (string.IsNullOrWhiteSpace(positionState))
        {
            positionState = "Unknown";
        }

        var matchingOrders = FindMatchingActiveOrders();

        string pQty = positionState == "Unknown" ? "Unknown" : "0";
        string pPrice = "Unknown";
        if (position != null && Math.Abs(position.Quantity) > 0)
        {
            pQty = position.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            pPrice = position.OpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var activeOrdersCount = CountActiveOrders();
        var activeTestOrdersCount = FindActiveTestOrders().Length;
        var matchingOrdersCount = matchingOrders.Length;

        Order? snapshotOrder = order;
        if (snapshotOrder == null && matchingOrdersCount == 1)
        {
            snapshotOrder = matchingOrders[0];
        }

        string defaultStr = matchingOrdersCount > 1 ? "Ambiguous" : "Unknown";

        var oId = snapshotOrder != null ? snapshotOrder.Id : (orderHistory != null ? orderHistory.Id : defaultStr);
        var oStatus = snapshotOrder != null ? snapshotOrder.Status.ToString() : (orderHistory != null ? orderHistory.Status.ToString() : defaultStr);
        var oComment = snapshotOrder != null ? snapshotOrder.Comment : (orderHistory != null ? orderHistory.Comment : defaultStr);
        var oPrice = snapshotOrder != null ? snapshotOrder.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : defaultStr);
        var oTQty = snapshotOrder != null ? snapshotOrder.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : defaultStr);
        var oFQty = snapshotOrder != null ? snapshotOrder.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : defaultStr);
        var oAvgP = snapshotOrder != null ? snapshotOrder.AverageFillPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.AverageFillPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : defaultStr);

        var threadId = Thread.CurrentThread.ManagedThreadId;
        var accName = TestAccount != null ? Quote(TestAccount.Name) : "Unknown";
        var obsFin = _observationFinished ? " ObservationFinished=True" : "";

        return $"ProviderTime={providerTime} " +
               $"RunId=[{_runId}] TargetMarker=[{_targetMarker}] Scenario=[{Scenario}] Source=[{source}] Event=[{eventName}] " +
               $"AccountId={Quote(TestAccount?.Id)} AccountName={accName} SymbolId={Quote(TestSymbol?.Id)} " +
               $"ActiveOrdersCount={activeOrdersCount} ActiveTestOrdersCount={activeTestOrdersCount} MatchingOrdersCount={matchingOrdersCount} " +
               $"PositionState={positionState} PositionQty={pQty} PositionOpenPrice={pPrice} " +
               $"OrderId={Quote(oId)} Status={Quote(oStatus)} Comment={Quote(oComment)} " +
               $"Price={oPrice} TotalQty={oTQty} FilledQty={oFQty} AverageFillPrice={oAvgP}{obsFin}" +
               (string.IsNullOrEmpty(extraInfo) ? "" : $" {extraInfo}") +
               $" ThreadId={threadId}";
    }

    private void WriteLog(string source, string eventName, string providerTime = "Unknown", string extraInfo = "")
    {
        var line = BuildSnapshotLine(source, eventName, providerTime: providerTime, extraInfo: extraInfo);
        WriteLogLine(line);
    }

    private void WriteLogLine(string contentLine)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        lock (_logLock)
        {
            var seq = ++_logSequence;
            var ts = DateTime.UtcNow.ToString("O");
            var fullLine = $"Sequence={seq} ObservedAtUtc={ts} {contentLine}";
            File.AppendAllText(_logPath, fullLine + Environment.NewLine);
        }
    }
}
