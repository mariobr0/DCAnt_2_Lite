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

    [InputParameter("Scenario", 10, variants: new object[] { "PrepareActiveOrder", "PrepareActiveOrder", "PrepareOpenPosition", "PrepareOpenPosition", "ObserveRecovery", "ObserveRecovery" })]
    public string Scenario = "ObserveRecovery";

    [InputParameter("Target Marker (for ObserveRecovery)", 20)]
    public string InputTargetMarker = "";

    [InputParameter("Expected Order Id", 25)]
    public string InputExpectedOrderId = "";

    [InputParameter("Expected Order Price", 26, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double InputExpectedOrderPrice = 0.0;

    [InputParameter("Expected Order Total Quantity", 27, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double InputExpectedOrderTotalQuantity = 0.0;

    [InputParameter("Expected Position Quantity", 28, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double InputExpectedPositionQuantity = 0.0;

    [InputParameter("Expected Position Open Price", 29, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double InputExpectedPositionOpenPrice = 0.0;

    [InputParameter("Test Price (Place...)", 40, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double TestPrice = 0.0;

    [InputParameter("Test Quantity", 50, minimum: 0.0, maximum: 1000000.0, increment: 0.00000001, decimalPlaces: 8)]
    public double TestQuantity = 1.0;

    [InputParameter("Test Side", 60)]
    public Side TestSide = Side.Buy;

    private string _runId = "";
    private string _targetMarker = "";
    private string _logPath = "";
    private readonly object _logLock = new object();
    private readonly object _eventProcessingLock = new object();

    private volatile bool _stopping;
    private long _logSequence = 0;

    // Baseline state (immutable)
    private string _baselineOrderId = "Unknown";
    private string _baselineOrderStatus = "Unknown";
    private string _baselineFilledQty = "Unknown";
    private string _baselineComment = "Unknown";
    private string _baselinePositionQty = "Unknown";
    private string _baselinePositionOpenPrice = "Unknown";

    // Last Observed State (mutable)
    private string _lastOrderId = "Unknown";
    private string _lastOrderStatus = "Unknown";
    private string _lastFilledQty = "Unknown";
    private string _lastComment = "Unknown";
    private string _lastPositionQty = "Unknown";
    private string _lastPositionOpenPrice = "Unknown";
    private string _lastConnectionState = "Unknown";

    private string _recoveryPhase = "Unknown";

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
            _logSequence = 0;
            _recoveryPhase = "Starting";

            ValidateCommonInputs();
            GenerateRunId();

            var logsDir = @"C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\logs";
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
            _logPath = Path.Combine(logsDir, $"DCAnt2_Contracts_{_runId}.log");

            if (Scenario == "PrepareActiveOrder" || Scenario == "PrepareOpenPosition")
            {
                GenerateTargetMarker();
                WriteLog("System", "TARGET_MARKER_CREATED", extraInfo: $"TargetMarker={Quote(_targetMarker)}");

                ExecutePlaceScenario();

                global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded += OnOrderEvent;
                global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved += OnOrderEvent;
                global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded += OnOrderHistoryEvent;
                global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded += OnPositionEvent;
                global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved += OnPositionEvent;

                _recoveryPhase = "Observing";
                WriteLogLine(BuildSnapshotLine("Strategy", "StartSnapshot"));
            }
            else if (Scenario == "ObserveRecovery")
            {
                _targetMarker = InputTargetMarker;
                InitializeObserveRecovery();
            }
            else
            {
                WriteLog("Strategy", "UnknownScenario", extraInfo: $"Message=\"Unknown Scenario: {Scenario}. Defaulting to nothing.\"");
            }
        }
        catch (Exception ex)
        {
            WriteLog("Strategy", "Error", extraInfo: $"Exception={Quote(ex.ToString())}");
            Stop();
        }
    }

    private void InitializeObserveRecovery()
    {
        lock (_eventProcessingLock)
        {
            _recoveryPhase = "Initializing";

            global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded += OnOrderEvent;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved += OnOrderEvent;
            global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded += OnOrderHistoryEvent;
            global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded += OnPositionEvent;
            global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved += OnPositionEvent;

            var order = FindExpectedOrder();
            var pos = ReadPosition(out string posState);

            _baselineOrderId = order?.Id ?? "Unknown";
            _baselineOrderStatus = order?.Status.ToString() ?? "Unknown";
            _baselineFilledQty = order?.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown";
            _baselineComment = order?.Comment ?? "Unknown";

            _baselinePositionQty = pos != null ? pos.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (posState == "Unknown" ? "Unknown" : "0");
            _baselinePositionOpenPrice = pos != null ? pos.OpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";

            _lastOrderId = _baselineOrderId;
            _lastOrderStatus = _baselineOrderStatus;
            _lastFilledQty = _baselineFilledQty;
            _lastComment = _baselineComment;

            _lastPositionQty = _baselinePositionQty;
            _lastPositionOpenPrice = _baselinePositionOpenPrice;

            _lastConnectionState = GetConnectionStateSafe();

            var line = BuildSnapshotLine("ObserveRecovery", "StartSnapshot", order, pos, posState, null, "Unknown", "Classification=CurrentSnapshot");
            WriteLogLine(line);

            _recoveryPhase = "Observing";
        }
    }

    protected override void OnStop()
    {
        _stopping = true;

        global::TradingPlatform.BusinessLayer.Core.Instance.OrderAdded -= OnOrderEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrderRemoved -= OnOrderEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.OrdersHistoryAdded -= OnOrderHistoryEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.PositionAdded -= OnPositionEvent;
        global::TradingPlatform.BusinessLayer.Core.Instance.PositionRemoved -= OnPositionEvent;

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
        var finalSnapshot = BuildSnapshotLine("Strategy", "OnStop", null, position, positionState, null, "Unknown", extra);

        WriteLogLine(finalSnapshot);
    }

    private void OnOrderEvent(Order order)
    {
        lock (_eventProcessingLock)
        {
            if (_stopping) return;
            if (order.Account?.Id != TestAccount?.Id || order.Symbol?.Id != TestSymbol?.Id) return;

            bool markerMatches = !string.IsNullOrEmpty(_targetMarker) && order.Comment == _targetMarker;
            bool orderIdMatches = !string.IsNullOrWhiteSpace(InputExpectedOrderId) && order.Id == InputExpectedOrderId;
            if (!markerMatches && !orderIdMatches) return;

            string oId = order.Id;
            string oStatus = order.Status.ToString();
            string oFilledQty = order.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string oComment = order.Comment ?? "Unknown";

            string classification = "Unknown";

            if (_recoveryPhase == "Observing")
            {
                if (oId == _lastOrderId && oStatus == _lastOrderStatus && oFilledQty == _lastFilledQty && oComment == _lastComment)
                {
                    classification = "RepeatedStateNotification";
                }
                else
                {
                    classification = "NewStateChange";
                }

                _lastOrderId = oId;
                _lastOrderStatus = oStatus;
                _lastFilledQty = oFilledQty;
                _lastComment = oComment;
            }

            CheckConnectionStateTrigger();

            string extra = $"Classification={classification}";
            var line = BuildSnapshotLine("OrderEvent", "OrderState", order, null, null, null, "Unknown", extra);
            WriteLogLine(line);
        }
    }

    private void OnOrderHistoryEvent(OrderHistory orderHistory)
    {
        lock (_eventProcessingLock)
        {
            if (_stopping) return;
            if (orderHistory.Account?.Id != TestAccount?.Id || orderHistory.Symbol?.Id != TestSymbol?.Id) return;

            bool markerMatches = !string.IsNullOrEmpty(_targetMarker) && orderHistory.Comment == _targetMarker;
            bool orderIdMatches = !string.IsNullOrWhiteSpace(InputExpectedOrderId) && orderHistory.Id == InputExpectedOrderId;
            if (!markerMatches && !orderIdMatches) return;

            string oId = orderHistory.Id;
            string oStatus = orderHistory.Status.ToString();
            string oFilledQty = orderHistory.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string oComment = orderHistory.Comment ?? "Unknown";

            string classification = "Unknown";
            if (_recoveryPhase == "Observing")
            {
                if (oId == _lastOrderId && oStatus == _lastOrderStatus && oFilledQty == _lastFilledQty && oComment == _lastComment)
                {
                    classification = "RepeatedStateNotification";
                }
                else
                {
                    classification = "NewStateChange";
                }

                _lastOrderId = oId;
                _lastOrderStatus = oStatus;
                _lastFilledQty = oFilledQty;
                _lastComment = oComment;
            }

            CheckConnectionStateTrigger();

            string extra = $"Classification={classification}";
            var line = BuildSnapshotLine("OrderHistoryEvent", "OrderHistoryState", null, null, null, orderHistory, "Unknown", extra);
            WriteLogLine(line);
        }
    }

    private void OnPositionEvent(Position position)
    {
        lock (_eventProcessingLock)
        {
            if (_stopping) return;
            if (position.Account?.Id != TestAccount?.Id || position.Symbol?.Id != TestSymbol?.Id) return;

            string pQty = position.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string pPrice = position.OpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);

            string classification = "Unknown";
            if (_recoveryPhase == "Observing")
            {
                if (pQty == _lastPositionQty && pPrice == _lastPositionOpenPrice)
                {
                    classification = "RepeatedStateNotification";
                }
                else
                {
                    classification = "NewStateChange";
                }

                _lastPositionQty = pQty;
                _lastPositionOpenPrice = pPrice;
            }

            CheckConnectionStateTrigger();

            string extra = $"Classification={classification}";
            var line = BuildSnapshotLine("PositionEvent", "PositionState", null, position, null, null, "Unknown", extra);
            WriteLogLine(line);
        }
    }

    private void CheckConnectionStateTrigger()
    {
        string currentConnState = GetConnectionStateSafe();
        if (currentConnState != _lastConnectionState && currentConnState != "Unknown")
        {
            string triggerState = currentConnState;
            _lastConnectionState = triggerState;
            TriggerProbes(triggerState);
        }
    }

    private void TriggerProbes(string triggerConnectionState)
    {
        ScheduleProbe(100, triggerConnectionState);
        ScheduleProbe(250, triggerConnectionState);
        ScheduleProbe(500, triggerConnectionState);
        ScheduleProbe(1000, triggerConnectionState);
        ScheduleProbe(2000, triggerConnectionState);
        ScheduleProbe(5000, triggerConnectionState);
    }

    private void ScheduleProbe(int delayMs, string triggerConnectionState)
    {
        Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            if (_stopping) return;
            try
            {
                var pos = ReadPosition(out string pState);
                var order = FindExpectedOrder();
                string obsConnState = GetConnectionStateSafe();

                string extra = $"Classification=Unknown TriggerConnectionState={triggerConnectionState} ProbeDelayMs={delayMs} ObservedConnectionState={obsConnState}";
                WriteLogLine(BuildSnapshotLine("Probe", "RecoveryProbe", order, pos, pState, null, "Unknown", extra));
            }
            catch (Exception ex)
            {
                if (!_stopping) WriteLog("Probe", "Error", extraInfo: $"Probe {delayMs}ms failed: {Quote(ex.Message)}");
            }
        });
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
        if (positionState == "Open" || (position != null && Math.Abs(position.Quantity) > 0))
        {
            WriteLog("Strategy", "POSITION_CHECK", extraInfo: $"Result=Open PositionQty={position?.Quantity} PlaceAllowed=False");
            throw new InvalidOperationException($"Place forbidden. Position is not zero. Found: {position?.Quantity}");
        }

        var activeOrders = FindActiveTestOrders();
        if (activeOrders.Length > 0)
        {
            throw new InvalidOperationException($"Place forbidden. Found {activeOrders.Length} active qtct_ orders");
        }

        WriteLog("Strategy", "POSITION_CHECK", extraInfo: "Result=None PositionQty=0 ActiveTestOrdersCount=0 PlaceAllowed=True");
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

        WriteLog("Strategy", "AfterApiCall", extraInfo: $"Classification=Unknown PlaceOrderStatus={result.Status} Message={Quote(result.Message)} ReturnedOrderId={Quote(result.OrderId)} ElapsedMs={watch.ElapsedMilliseconds}");
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

    private Order? FindExpectedOrder()
    {
        var orders = global::TradingPlatform.BusinessLayer.Core.Instance.Orders.Where(o =>
            o.Account.Id == TestAccount?.Id &&
            o.Symbol.Id == TestSymbol?.Id &&
            ((!string.IsNullOrEmpty(_targetMarker) && o.Comment == _targetMarker) ||
             (!string.IsNullOrWhiteSpace(InputExpectedOrderId) && o.Id == InputExpectedOrderId))
        ).ToArray();
        return orders.FirstOrDefault();
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

    private string GetConnectionStateSafe()
    {
        try
        {
            if (TestAccount != null && TestAccount.Connection != null)
            {
                return TestAccount.Connection.State.ToString();
            }
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetConnectionIdSafe()
    {
        try
        {
            if (TestAccount != null && TestAccount.Connection != null)
            {
                return TestAccount.Connection.Id ?? "Unknown";
            }
            return "Unknown";
        }
        catch
        {
            return "Unknown";
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

        string pQty = positionState == "Unknown" ? "Unknown" : "0";
        string pPrice = "Unknown";
        if (position != null && Math.Abs(position.Quantity) > 0)
        {
            pQty = position.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            pPrice = position.OpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var activeOrdersCount = CountActiveOrders();
        var activeTestOrdersCount = FindActiveTestOrders().Length;

        Order? snapshotOrder = order ?? FindExpectedOrder();

        var oId = snapshotOrder != null ? snapshotOrder.Id : (orderHistory != null ? orderHistory.Id : "Unknown");
        var oStatus = snapshotOrder != null ? snapshotOrder.Status.ToString() : (orderHistory != null ? orderHistory.Status.ToString() : "Unknown");
        var oComment = snapshotOrder != null ? snapshotOrder.Comment : (orderHistory != null ? orderHistory.Comment : "Unknown");
        var oPrice = snapshotOrder != null ? snapshotOrder.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.Price.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oTQty = snapshotOrder != null ? snapshotOrder.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.TotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");
        var oFQty = snapshotOrder != null ? snapshotOrder.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : (orderHistory != null ? orderHistory.FilledQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown");

        string orderPresent = (snapshotOrder != null) ? "True" : "False";

        string connId = GetConnectionIdSafe();
        string connState = GetConnectionStateSafe();

        // Expected matches
        string expOrderIdMatches = "Unknown";
        if (!string.IsNullOrWhiteSpace(InputExpectedOrderId))
            expOrderIdMatches = (oId == InputExpectedOrderId).ToString();

        string expPriceMatches = "Unknown";
        if (InputExpectedOrderPrice > 0)
            expPriceMatches = (snapshotOrder != null && Math.Abs(snapshotOrder.Price - InputExpectedOrderPrice) < 1e-8).ToString();

        string expTotalQtyMatches = "Unknown";
        if (InputExpectedOrderTotalQuantity > 0)
            expTotalQtyMatches = (snapshotOrder != null && Math.Abs(snapshotOrder.TotalQuantity - InputExpectedOrderTotalQuantity) < 1e-8).ToString();

        string expPosQtyMatches = "Unknown";
        if (InputExpectedPositionQuantity > 0)
            expPosQtyMatches = (position != null && Math.Abs(position.Quantity - InputExpectedPositionQuantity) < 1e-8).ToString();

        string expPosOpenPriceMatches = "Unknown";
        if (InputExpectedPositionOpenPrice > 0)
            expPosOpenPriceMatches = (position != null && Math.Abs(position.OpenPrice - InputExpectedPositionOpenPrice) < 1e-8).ToString();

        var threadId = Thread.CurrentThread.ManagedThreadId;
        var accName = TestAccount != null ? Quote(TestAccount.Name) : "Unknown";

        string expOrderIdStr = string.IsNullOrWhiteSpace(InputExpectedOrderId) ? "Unknown" : Quote(InputExpectedOrderId);
        string expPriceStr = InputExpectedOrderPrice > 0 ? InputExpectedOrderPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";
        string expTotalQtyStr = InputExpectedOrderTotalQuantity > 0 ? InputExpectedOrderTotalQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";
        string expPosQtyStr = InputExpectedPositionQuantity > 0 ? InputExpectedPositionQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";
        string expPosPriceStr = InputExpectedPositionOpenPrice > 0 ? InputExpectedPositionOpenPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown";

        return $"ProviderTime={providerTime} " +
               $"RunId=[{_runId}] TargetMarker=[{_targetMarker}] Scenario=[{Scenario}] RecoveryPhase=[{_recoveryPhase}] Source=[{source}] Event=[{eventName}] " +
               $"ConnectionId={Quote(connId)} ConnectionState={Quote(connState)} " +
               $"AccountId={Quote(TestAccount?.Id)} AccountName={accName} SymbolId={Quote(TestSymbol?.Id)} " +
               $"ExpectedOrderId={expOrderIdStr} ExpectedOrderPrice={expPriceStr} ExpectedOrderTotalQuantity={expTotalQtyStr} ExpectedPositionQuantity={expPosQtyStr} ExpectedPositionOpenPrice={expPosPriceStr} " +
               $"ExpectedOrderIdMatches={expOrderIdMatches} ExpectedOrderPriceMatches={expPriceMatches} ExpectedOrderTotalQuantityMatches={expTotalQtyMatches} ExpectedPositionQuantityMatches={expPosQtyMatches} ExpectedPositionOpenPriceMatches={expPosOpenPriceMatches} " +
               $"OrderPresentInActiveCollection={orderPresent} " +
               $"PositionState={positionState} PositionQty={pQty} PositionOpenPrice={pPrice} " +
               $"OrderId={Quote(oId)} Status={Quote(oStatus)} Comment={Quote(oComment)} " +
               $"OrderPrice={oPrice} OrderTotalQuantity={oTQty} FilledQuantity={oFQty}" +
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
