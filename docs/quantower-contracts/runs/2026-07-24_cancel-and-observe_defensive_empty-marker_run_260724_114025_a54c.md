# Quantower Contract Test Run: CancelAndObserve (Defensive: Empty Marker)

## Environment
- Scenario: CancelAndObserve
- RunId: run_260724_114025_a54c
- TargetMarker: (Empty)
- Quantower version: Unknown (Test aborted at startup)
- Connector: Unknown

## Result

Passed (Defensive rejection)

## Evidence quality

High

## Final state

- Final order state: N/A (No order was targeted)
- Final active matching orders: 0
- Final position state: None
- Manual cleanup required: No

## Observations

- Strategy successfully threw `InvalidOperationException: TargetMarker is required for ObserveOnly or CancelAndObserve`.
- The test safely aborted during `OnRun()` before attempting any API calls or searches.
- No side effects were observed.

### Timeline

| Event | Time offset | Key observations |
|---|---|---|
| `Error` | `+0 ms` | `TargetMarker is required for ObserveOnly or CancelAndObserve` |
| `OnStop` | `+1.0 ms` | Strategy stopped. `ActiveOrdersCount=0`, `ManualCleanupRequired=No`. |
