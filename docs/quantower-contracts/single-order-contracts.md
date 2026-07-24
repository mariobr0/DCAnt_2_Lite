# Quantower Single Order Contracts

## Overview
This document tracks the verified behavior of Quantower API specifically related to placing, modifying, and canceling single orders, as well as the visibility of these orders in the `Core.Orders` and `Core.OrdersHistory` collections and related callbacks.

## Contracts

### QT-PLACE-001: Local PlaceOrder result precedes observable order state
**Status:** Confirmed for the tested environment
**Observation:** `PlaceOrder` returned local status `Success` and a non-empty `ReturnedOrderId`. The immediate snapshot after the API return still contained zero matching active orders. The order became observable afterward through `OrderAdded`.
**Conclusion:** A local `Success` result does not prove that the order is already present in `Core.Orders` or that its final provider state is known.
**Architectural consequence:** Store the local API result separately from the observed order state. Do not mark a managed order as confirmed-open based only on `PlaceOrder Success`.
**References**:
- [run_260724_100206_3cb1](runs/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.md)
- [Evidence Log](evidence/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.log)
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-PLACE-002: Order visibility is observed after the local API result
**Status:** Confirmed for this run
**Observation:** The immediate snapshot after `PlaceOrder` contained no matching order. `OrderAdded` was observed approximately 54.9 ms after the API result. A 250 ms probe also found the order in `Core.Orders`.
**Conclusion:** Order visibility is asynchronous relative to the local API result.
**Limitations:** The observed delay is not a fixed guarantee and may vary by load, connector, connection state and Quantower version.
**Architectural consequence:** Prefer an observed callback or reconciliation. Do not use the measured 54.9 ms, or any derived fixed sleep such as 50â€“100 ms, as proof that the order exists.
**References**:
- [run_260724_100206_3cb1](runs/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.md)
- [Evidence Log](evidence/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.log)
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-ID-001: ReturnedOrderId corresponds to the observed Order.Id
**Status:** Confirmed for Place, Opened, and Filled scenarios
**Observation:** The same value was observed in:
- `PlaceOrder ReturnedOrderId`;
- `OrderAdded Order.Id`;
- `OrdersHistoryAdded Order.Id`;
- `Core.Orders Order.Id`.
**Conclusion:** For this connector and test run, `ReturnedOrderId` corresponded to the observable Quantower `Order.Id`.
**Limitations:** Stability across reconnect, restart, modification and other connectors has not yet been tested.
**Architectural consequence:** The value is a candidate for correlation with the provider order object. Its full lifecycle contract must be verified before treating it as a universally stable broker identifier.
**References**:
- [run_260724_100206_3cb1](runs/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.md)
- [Evidence Log](evidence/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.log)
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-ID-002: Comment is preserved during the tested Place lifecycle
**Status:** Confirmed for Place, Opened, and Filled states
**Observation:** The exact `TargetMarker` was preserved in:
- `OrderAdded`;
- `OrdersHistoryAdded` with `Status=Opened` and `Status=Filled`;
- `Core.Orders`;
- `OrderRemoved`.
**Conclusion:** Comment can be used as a diagnostic correlation signal during the tested Place/Open scenario.
**Limitations:** Reliability after Cancel, fill, reconnect, restart, ModifyOrder and on other connectors is not yet confirmed.
**Architectural consequence:** Comment may participate in order correlation, but it must not yet be the only ownership key in production code.
**References**:
- [run_260724_100206_3cb1](runs/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.md)
- [Evidence Log](evidence/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.log)
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-CANCEL-001: Cancel confirmation
**Status:** Confirmed for the tested environment

**Observation:**
- `CancelOrder` returned local status `Success`.
- The immediate snapshot still showed the order as active and Opened.
- `OrderRemoved` was observed approximately 63.65 ms later and the order disappeared from the active collection.
- The object received through `OrderRemoved` still had `Status=Opened`.
- `OrdersHistoryAdded` was observed approximately 65.04 ms after the API result with `Status=Cancelled`.

**Conclusion:**
Local `CancelOrder Success` is not sufficient confirmation of cancellation. In this test, removal from the active collection was observed through `OrderRemoved`, while the explicit terminal status `Cancelled` was observed separately through `OrdersHistoryAdded`.

**Architectural consequence:**
Do not permit replacement immediately after local Cancel success. Wait for observed cancellation evidence or reconciliation. Do not expect `OrderRemoved` itself to contain the terminal Cancelled status.

**References**:
- [run_260724_112811_3cff](runs/2026-07-24_cancel-and-observe_success_run_260724_112811_3cff.md)
- [Evidence Log](evidence/2026-07-24_cancel-and-observe_success_run_260724_112811_3cff.log)

### QT-CANCEL-002: State changes after Cancel request
**Status:** Confirmed for asynchronous state changes; fill-race behavior remains NotTested

**Observation:**
Immediately after local Cancel success, the order was still active with `Status=Opened`. Later, it was removed from the active collection and reported as `Cancelled` through `OrdersHistoryAdded`.
No fill, quantity change or position change was observed during this test.

**Conclusion:**
The order lifecycle continues asynchronously after the local Cancel result. This test does not prove that fills after a Cancel request are impossible.

**Architectural consequence:**
Keep the old order registered while cancellation is pending. Continue accepting relevant late events until cancellation is confirmed and the broker state is reconciled.

**References**:
- [run_260724_112811_3cff](runs/2026-07-24_cancel-and-observe_success_run_260724_112811_3cff.md)
- [Evidence Log](evidence/2026-07-24_cancel-and-observe_success_run_260724_112811_3cff.log)

## Safe Replacement Lifecycle
Based on the validated contracts, replacing a TakeProfit (TP) or StopLoss (SL) must strictly follow an asynchronous state machine.

**UNSAFE (DO NOT DO THIS):**
1. `CancelOrder` returns local `Success`.
2. Delete the old order from internal state.
3. Immediately `PlaceOrder` for the new one.

**OBSERVED SAFE PROCESS:**
1. **Cancel requested**: Call `CancelOrder`.
2. **Local Success**: `CancelOrder` returns `Success`.
3. **Pending State**: The old order must remain in a `CancelPending` state internally.
4. **Active Removal**: Await `OrderRemoved` (or reconciliation) to confirm absence in `Core.Orders`.
5. **Terminal Confirmation**: Await `OrdersHistoryAdded` to confirm `Cancelled` (if available).
6. **Replacement**: Only after the above confirmations is it safe to allow `PlaceOrder` for the replacement.

## Cross-run ordering observation

Order-state and position callbacks were observed in different orders across test runs.

In one run, the terminal order state preceded `PositionAdded`.
In another Binance Futures run, `PositionEvent` preceded the first observed `Filled` order callback by approximately 4.34 ms.

**Architectural consequence:**
Production logic must not require a fixed ordering between order and position callbacks. The system must treat order state and position snapshots as independent observations and reconcile them if they temporarily diverge.

## Additional observations
- `OrdersHistoryAdded` was observed for `Status=Opened` with `FilledQty=0`.
- Therefore, `OrdersHistoryAdded` must not be interpreted as a financial execution solely from the event name.

### QT-FILL-001: FilledQuantity behavior
**Status:** Confirmed
**Observation:** In PlaceForPartialFill, FilledQuantity continuously increases on each partial fill event. with TotalQty=10 and Status=Filled.
**Conclusion:** A single full fill does not allow distinguishing whether FilledQuantity represents a cumulative total or the quantity of the latest fill. Both explanations yield 10. ObservedDifference=10 is only a diagnostic difference, not a separate execution event.

**Architectural consequence:** Delta must be manually calculated by tracking `PreviousFilledQty` if delta semantics are needed.
**References**:
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-FILL-002: AverageFillPrice
**Status:** Inconclusive
**Observation:** In this test, AverageFillPrice=0.10674 matched exactly with PositionOpenPrice=0.10674.
**Conclusion:** A single full fill confirms equality, but does not prove whether AverageFillPrice is a cumulative average or the price of the latest fill.

**Architectural consequence:** Do not treat `AverageFillPrice` as the price of a single transaction.
**References**:
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-FILL-003: Separate Trade/Execution source
**Status:** NotSupported in the examined public surface of Core (Quantower v1.146.16)
**Observation:** ExecutionAdded and TradeAdded were not publicly exposed by TradingPlatform.BusinessLayer.Core in this API version.
**Conclusion:** There are no separate public Trade or Execution events available through the standard Core surface in this environment.
**Limitations:** Data might be available via another public collection, history, or connector-specific API.
**Architectural consequence:** Do not attempt to subscribe to ExecutionAdded or TradeAdded via reflection.
**References**:
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-FILL-004: Stable Execution ID
**Status:** NotSupported or Inconclusive
**Observation:** Without a separate Trade/Execution source, a distinct Execution ID cannot be obtained.
**Conclusion:** OrderId cannot be used as an Execution ID.
**Architectural consequence:** Do not invent exec_<OrderId>.
**References**:
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-FILL-005: Callback order on full fill
**Status:** Confirmed for this run
**Observation:** The observed sequence was: OrderAdded/Open -> OrdersHistoryAdded/Open -> OrderRemoved/Open -> OrdersHistoryAdded/Filled -> PositionAdded. Crucially, OrderRemoved maintained Status=Opened.
**Conclusion:** OrderRemoved does not provide the final filled state. OrdersHistoryAdded provides the final Status=Filled.
**Architectural consequence:** Do not rely on OrderRemoved for the filled state.
**References**:
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-POS-001: Position update timing
**Status:** Confirmed for this run
**Observation:** Core.Positions showed no position during the OrdersHistoryAdded/Filled callback. Approximately 6.26 ms later, PositionAdded fired.
**Conclusion:** The position is updated via a separate asynchronous event after the terminal order-history callback.
**Architectural consequence:** EngineLoop must not assume Core.Positions is updated immediately inside the order Filled callback. A separate check or reconciliation is needed.
**References**:
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-POS-002: Position.OpenPrice
**Status:** Confirmed only for a single full fill
**Observation:** AverageFillPrice=0.10674 matched PositionOpenPrice=0.10674.
**Conclusion:** Equality holds for a single fill.
**Limitations:** Semantics after multiple partial fills are not proven.
**Architectural consequence:** Needs partial fill testing.
**References**:
- [run_260724_141120_58fa](runs/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.md)
- [Evidence Log](evidence/2026-07-24_PlaceForFullFill_run_260724_141120_58fa.log)

### QT-DUP-001: Event duplication
**Status:** Confirmed for duplicate order-state delivery
**Observation:** In a Binance Futures run, after the first `OrderEvent/Filled` callback (`NewStateChange`), a second identical `OrderEvent/Filled` callback arrived, followed by `OrdersHistoryAdded/Filled`. Both subsequent callbacks were correctly classified as `RepeatedStateNotification`.
**Conclusion:** A single executed order can generate multiple `Filled` callbacks.
**Architectural consequence:** Retain idempotency protection. Multiple `Filled` notifications must not be treated as multiple financial executions. Baseline and last-observed states must filter out these repetitions.
**References**:
- [run_260724_180714_e6c0](runs/2026-07-24_prepare-open-position_run_260724_180714_e6c0.md)

### QT-STRATEGY-RESTART-001: Active order visibility after Strategy restart

**Status:** Confirmed for the tested environment

**Observation:**

After stopping `PrepareActiveOrder` and starting a new `ObserveRecovery` instance, the existing order was present in the initial snapshot:

- OrderPresentInActiveCollection: True
- OrderId: d502dd3a-32d7-4bb6-b65e-0f477d7be7b7
- Status: Opened
- Comment: qtct_240726_d0a29466
- OrderPrice: 0.076
- OrderTotalQuantity: 10
- FilledQuantity: 0

The observed OrderId and OrderPrice matched their expected values.

**Conclusion:**

For the tested environment, restarting the diagnostic Strategy did not lose the active order. The order was available as a current snapshot immediately when the new Strategy instance started.

**Architectural consequence:**

An order found in the initial recovery snapshot must be treated as existing external state, not as a new order event.

**References**:
- [Evidence Log](evidence/2026-07-24_observe-recovery-cleanup_run_260724_154648_866a.log)

### QT-RECOVERY-POS-001: Position visibility after Strategy and Quantower restart

**Status:** Confirmed for the tested environment

**Observation:**
After both stopping the strategy (Scenario D) and a full Quantower restart with connection recovery (Scenario E), the existing Binance Futures position was immediately available in the initial `ObserveRecovery` snapshot:
- PositionState: Open
- PositionQuantity: 200
- PositionOpenPrice: 0.0458
- Classification: CurrentSnapshot

No additional position callback was required for the Strategy to observe the current position.
The terminal restart test does not establish exactly when the position became available during terminal startup because `ObserveRecovery` was started after the connection had reached `Connected` state.

**Conclusion:**
For the tested environment, an existing open position is available as a current snapshot immediately when the new Strategy instance starts, regardless of whether the strategy or the entire terminal was restarted.
The startup position snapshot is existing external state and must not be interpreted as a new fill.

**Architectural consequence:**
An open position found in the initial recovery snapshot must be treated as the absolute current state. EngineLoop must be able to adopt this position gracefully.

**References**:
- [run_260724_180912_c98e](runs/2026-07-24_observe-recovery_strategy-restart-open-position_run_260724_180912_c98e.md)
- [run_260724_181340_5fb8](runs/2026-07-24_observe-recovery_terminal-restart-open-position_run_260724_181340_5fb8.md)
- [Evidence Log (Strategy)](evidence/2026-07-24_observe-recovery_strategy-restart-open-position_run_260724_180912_c98e.log)
- [Evidence Log (Terminal)](evidence/2026-07-24_observe-recovery_terminal-restart-open-position_run_260724_181340_5fb8.log)

### QT-RECOVERY-POS-002: Preservation of position quantity and open price

**Status:** Confirmed for the tested run

**Observation:**
The preparation evidence before restart contained:
- PositionQuantity: 200
- PositionOpenPrice: 0.0458

The initial snapshot after restart (both Strategy and Terminal) contained the exact same values.

**Limitation:**
ExpectedPositionQuantity and ExpectedPositionOpenPrice were not passed to `ObserveRecovery`. Equality was established by comparing the raw preparation and recovery logs rather than built-in comparison fields.

**Architectural consequence:**
The recovered position snapshot must be treated as the absolute current state. It replaces a stale local snapshot and must not be added to it.

**References**:
- [run_260724_180912_c98e](runs/2026-07-24_observe-recovery_strategy-restart-open-position_run_260724_180912_c98e.md)
- [run_260724_181340_5fb8](runs/2026-07-24_observe-recovery_terminal-restart-open-position_run_260724_181340_5fb8.md)
- [Evidence Log (Strategy)](evidence/2026-07-24_observe-recovery_strategy-restart-open-position_run_260724_180912_c98e.log)
- [Evidence Log (Terminal)](evidence/2026-07-24_observe-recovery_terminal-restart-open-position_run_260724_181340_5fb8.log)


### QT-STRATEGY-RESTART-002: Repeated callbacks after Strategy restart

**Status:** Inconclusive

**Observation:**

No order or order-history callbacks were observed between the initial snapshot and `OnStop` during this run.

**Conclusion:**

The active order was available through the startup snapshot, but no repeated callback was observed in this particular run.

**Limitations:**

Absence of a repeated callback in one run does not prove that such callbacks cannot occur.

**Architectural consequence:**

The system must still be prepared to handle repeated callbacks if they occur.

**References**:
- [Evidence Log](evidence/2026-07-24_observe-recovery_strategy-restart-active-order_run_260724_154349_d031.log)

### QT-CONNECTION-001: Active order state during reconnect

**Status:** Confirmed for the tested environment

**Observation:**
During the `Connecting` phase, the order briefly disappeared from the active collection (`OrderPresentInActiveCollection=False`). Once `Connected`, it became visible again (`OrderPresentInActiveCollection=True`) with its original parameters.

**Conclusion:**
Temporary unavailability of an order during a connection drop/reconnect must not be interpreted as a `Cancelled` state or a permanent loss of the order.

**Architectural consequence:**
The system must tolerate temporary order disappearance during reconnects. Do not automatically issue cancellation or replacement logic if an order vanishes while the connection is not fully established and stable.

**References**:
- [run_260724_172730_7709](runs/2026-07-24_observe-recovery_reconnect-active-order-binance_run_260724_172730_7709.md)
- [Evidence Log](evidence/2026-07-24_observe-recovery_reconnect-binance_run_260724_172730_7709.log)

### QT-CONNECTION-002: Preservation of active-order fields after reconnect

**Status:** Confirmed for the tested run

**Observation:**
The following values matched exactly before and after the reconnect:
- OrderId
- Comment
- OrderPrice
- OrderTotalQuantity
- Status
- FilledQuantity

**Conclusion:**
For the tested Binance Futures connection, order parameters including diagnostic `TargetMarker` (Comment) survive a reconnect and are accurately restored.

**Limitations:**
ExpectedOrderPrice and ExpectedOrderTotalQuantity were verified via log comparison, not built-in matches.

**Architectural consequence:**
The recovered OrderId and Comment are suitable correlation candidates for reconciliation after a reconnect.

**References**:
- [run_260724_172730_7709](runs/2026-07-24_observe-recovery_reconnect-active-order-binance_run_260724_172730_7709.md)
- [Evidence Log](evidence/2026-07-24_observe-recovery_reconnect-binance_run_260724_172730_7709.log)

### QT-TERMINAL-RESTART-001: Active order recovery after Quantower restart

**Status:** Confirmed for the tested environment

**Environment:**
- Quantower connector: Binance USDT-M Futures
- Symbol: ESPORTSUSDT
- Account type: Live

**Observation:**
Before terminal restart, the active order had:
- OrderId: 1854741719
- Comment: qtct_240726_2d8ef1ca
- Status: Opened
- OrderPrice: 0.039
- OrderTotalQuantity: 300
- FilledQuantity: 0

After completely restarting Quantower and starting a new ObserveRecovery Strategy instance, the same order was available in the initial snapshot with the same values.

**Conclusion:**
For the tested Binance Futures connection, the active external order was recoverable through Quantower after a full terminal restart.
The recovered order was observed as CurrentSnapshot and was not treated as a new order event.

**Architectural consequence:**
Initial Core.Orders state after terminal startup must be reconciled as existing external state. It must not trigger a repeated PlaceOrder or create a second local order.

**References**:
- [run_260724_175824_4a84](runs/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.md)
- [Evidence Log](evidence/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.log)

### QT-TERMINAL-RESTART-002: Preservation of active-order fields after restart

**Status:** Confirmed for the tested run

**Observation:**
The following values matched before and after the Quantower restart:
- OrderId
- Comment
- OrderPrice
- OrderTotalQuantity
- Status
- FilledQuantity

**Limitations:**
ExpectedOrderPrice and ExpectedOrderTotalQuantity were not passed to ObserveRecovery. Their equality was established by comparing the preparation and recovery raw logs, not by built-in comparison fields.

**Architectural consequence:**
The recovered OrderId and current order snapshot are suitable correlation candidates for reconciliation in this connector. Comment remains an additional correlation signal, not the only ownership key.

**References**:
- [run_260724_175824_4a84](runs/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.md)
- [Evidence Log](evidence/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.log)

### QT-RECOVERY-DUP-001: Repeated callbacks after terminal restart

**Status:** Inconclusive

**Observation:**
No repeated order, order-history or position callbacks were observed after terminal restart in this run. The order was available through CurrentSnapshot.

**Conclusion:**
Absence of repeated callbacks in this run does not prove that such callbacks cannot occur.

**Architectural consequence:**
The system must be prepared to handle repeated callbacks if they occur, even if they were not observed during this specific late-start strategy scenario.

**References**:
- [run_260724_175824_4a84](runs/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.md)
- [Evidence Log](evidence/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.log)

### QT-RECOVERY-FILL-001: Distinguishing recovered fills

**Status:** Confirmed in a narrow sense

**Observation:**
Recovered active order with FilledQuantity=0 was observed via CurrentSnapshot and was successfully isolated from new fill events.

**Limitations:**
The behavior of a recovered terminal `Filled` status was not tested in this run.

**Architectural consequence:**
The current snapshot correctly isolates pre-existing open states.

**References**:
- [run_260724_175824_4a84](runs/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.md)
- [Evidence Log](evidence/2026-07-24_observe-recovery_terminal-restart-binance_run_260724_175824_4a84.log)
