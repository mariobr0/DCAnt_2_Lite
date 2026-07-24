# Quantower Contract Test Run: PlaceAndObserve (Defensive: Active Order Exists)

## Environment
- Scenario: PlaceAndObserve
- RunId: run_260724_114237_6132
- TargetMarker: qtct_240726_c6b0f360 (Attempted to create)
- Quantower version: v1.146.16
- Connector: Trading Simulator

## Result

Passed (Defensive rejection)

## Evidence quality

High

## Final state

- Final order state: N/A (New order was not created)
- Final active matching orders: 0 (No orders match the newly generated marker)
- Final position state: None
- Manual cleanup required: Yes (The pre-existing `qtct_` test order must be cleaned up manually)

## Observations

- Strategy successfully detected `ActiveTestOrdersCount=1` in `ExecutePlaceScenario()`.
- It immediately threw `InvalidOperationException: Place forbidden. Found 1 active qtct_ orders`.
- No new API calls were made to `PlaceOrder`.
- Safely terminated and explicitly reported `CleanupReason=ActiveTestOrdersFound`.

### Timeline

| Event | Time offset | Key observations |
|---|---|---|
| `StartSnapshot` | `+0 ms` | Active test order found (`ActiveTestOrdersCount=1`). |
| `POSITION_CHECK` | `+5.6 ms` | Position check passed (`PositionState=None`, `PlaceAllowed=True`), moving to active order check. |
| `Error` | `+6.1 ms` | `Place forbidden. Found 1 active qtct_ orders`. |
| `OnStop` | `+6.5 ms` | Strategy stopped. `ManualCleanupRequired=Yes`. |
