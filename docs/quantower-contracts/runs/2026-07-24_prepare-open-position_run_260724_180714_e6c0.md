# Prepare Open Position (Binance)

**Scenario:** PrepareOpenPosition
**RunId:** run_260724_180714_e6c0
**Quantower version:** Unknown
**Connector:** Binance USDT-M Futures
**Account mode:** Live
**Symbol:** ESPORTSUSDT
**Git commit:** Unknown

## Environment
Targeting Binance USDT-M Futures via Quantower.
The `TargetMarker` was `qtct_240726_789f9fa1`. 
Symbol name and Symbol ID are both `ESPORTSUSDT`.

## Validation
- `RunId` is consistent across all sequences.
- `Scenario` and `TargetMarker` are correctly applied and aligned.
- Timestamps are strictly UTC.
- The order and position final states are clearly identifiable.

## Timeline
1. 18:07:14.534 `BeforeApiCall`
2. 18:07:14.828 `AfterApiCall` logged that the API returned a synchronous `Success` with `ReturnedOrderId="1854942790"`. The `ElapsedMs` was 293ms. The order was already observed in the active collection with `Status=Opened`.
3. 18:07:14.829 `StartSnapshot` logged the active order (`Status=Opened`).
4. 18:07:14.832 `OrderEvent` fired for `Status=Opened` (`NewStateChange`).
5. 18:07:23.786 `PositionEvent` fired for `PositionQty=200` (`NewStateChange`).
6. 18:07:23.791 `OrderEvent` fired for `Status=Filled` (`NewStateChange`).
7. 18:07:23.791 `OrderEvent` fired AGAIN for `Status=Filled` (`RepeatedStateNotification`).
8. 18:07:23.793 `OrderHistoryEvent` fired for `Status=Filled` (`RepeatedStateNotification`).
9. 18:08:13.955 `OnStop` was requested by the user.

*Note:* `PositionEvent` was observed approximately 8.95 seconds after the API result in this run, and roughly 4.34 milliseconds *before* the first `OrderEvent/Filled`.

## ID and Comment correlation
- `ReturnedOrderId` matches the generated `OrderId` ("1854942790") from all subsequent events.
- `Comment` matches `TargetMarker` ("qtct_240726_789f9fa1").

## Confirmed observations
- The `PlaceOrder` method returned synchronous local status `Success` with a populated `ReturnedOrderId`.
- A cross-run ordering anomaly was observed: the position callback arrived before the order state callback.
- Duplicate `OrderEvent/Filled` callbacks were received for the exact same execution. The strategy correctly classified the second occurrence as `RepeatedStateNotification`.
- `OrdersHistoryAdded` was observed with `Status=Filled`.

## Unknowns
None.

## Diagnostic issues
No diagnostic defects affecting this run were observed.

## Final state
- PositionState=Open
- PositionQty=200
- PositionOpenPrice=0.0458
- Active orders: 0
- ManualCleanupRequired=Yes

## Supported contracts
- **QT-DUP-001**: Confirmed for duplicate order-state delivery
- **Cross-run ordering observation**: Documented (Order vs Position callback order is not guaranteed).

## Result and next action
**Result**: Passed
**Evidence quality**: High
**Next action**: ReadyForNextScenario
