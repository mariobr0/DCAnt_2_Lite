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

### QT-PLACE-002: Order visibility is observed after the local API result
**Status:** Confirmed for this run
**Observation:** The immediate snapshot after `PlaceOrder` contained no matching order. `OrderAdded` was observed approximately 54.9 ms after the API result. A 250 ms probe also found the order in `Core.Orders`.
**Conclusion:** Order visibility is asynchronous relative to the local API result.
**Limitations:** The observed delay is not a fixed guarantee and may vary by load, connector, connection state and Quantower version.
**Architectural consequence:** Prefer an observed callback or reconciliation. Do not use the measured 54.9 ms, or any derived fixed sleep such as 50–100 ms, as proof that the order exists.
**References**:
- [run_260724_100206_3cb1](runs/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.md)
- [Evidence Log](evidence/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.log)

### QT-ID-001: ReturnedOrderId corresponds to the observed Order.Id
**Status:** Confirmed for the tested Place scenario
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

### QT-ID-002: Comment is preserved during the tested Place lifecycle
**Status:** Confirmed for Place and Opened state
**Observation:** The exact `TargetMarker` was preserved in:
- `OrderAdded`;
- `OrdersHistoryAdded` with `Status=Opened`;
- `Core.Orders`.
**Conclusion:** Comment can be used as a diagnostic correlation signal during the tested Place/Open scenario.
**Limitations:** Reliability after Cancel, fill, reconnect, restart, ModifyOrder and on other connectors is not yet confirmed.
**Architectural consequence:** Comment may participate in order correlation, but it must not yet be the only ownership key in production code.
**References**:
- [run_260724_100206_3cb1](runs/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.md)
- [Evidence Log](evidence/2026-07-24_PlaceAndObserve_run_260724_100206_3cb1.log)

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

## Additional observations
- `OrdersHistoryAdded` was observed for `Status=Opened` with `FilledQty=0`.
- Therefore, `OrdersHistoryAdded` must not be interpreted as a financial execution solely from the event name.
