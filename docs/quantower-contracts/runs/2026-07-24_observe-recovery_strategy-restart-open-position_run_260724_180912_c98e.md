# Observe Recovery - Strategy Restart with Open Position (Binance)

**Scenario:** ObserveRecovery (Scenario D)
**RunId:** run_260724_180912_c98e
**Quantower version:** Unknown
**Connector:** Binance USDT-M Futures
**Account mode:** Live
**Symbol:** ESPORTSUSDT
**Git commit:** Unknown

## Environment
Targeting Binance USDT-M Futures via Quantower.
`ExpectedPositionQuantity` and `ExpectedPositionOpenPrice` were not explicitly provided via UI parameters (left as Unknown), but the recovered position values were extracted via the initial `StartSnapshot`.

## Validation
- `RunId` is consistent (`run_260724_180912_c98e`).
- `Scenario` matches `ObserveRecovery`.
- Timestamps are UTC.
- Initial snapshot successfully captured the position.

## Timeline
1. 18:09:12.723 `StartSnapshot` recorded an existing open position (`PositionState=Open`, `PositionQty=200`, `PositionOpenPrice=0.0458`).
2. 18:10:02.563 `OnStop` was requested by the user.

Real interval between snapshot and stop: ~50 seconds.
No position callbacks (`PositionAdded`, `PositionUpdated`) and no order callbacks were observed during this period.

## ID and Comment correlation
- `OrderId` was not tracked via UI. The snapshot shows `OrderId="Unknown"`, `Status="Unknown"`, `Comment="Unknown"`, as it only read the position from `Core.Positions`.

## Confirmed observations
- The active open position was successfully populated in `Core.Positions` when the new diagnostic strategy instance started.
- The `PositionQty` (200) and `PositionOpenPrice` (0.0458) matched the parameters of the position opened in the previous test.
- No duplicate or repeated position callbacks occurred for the existing position during the observed 50 seconds.

## Unknowns
- `ExpectedPositionQuantityMatches` and `ExpectedPositionOpenPriceMatches` were `Unknown` due to lack of input parameters, though the raw values match the preparation log.

## Diagnostic issues
No diagnostic defects affecting this run were observed.

## Final state
- Position is still open.
- Order is filled (from previous run).
- ManualCleanupRequired=Yes (OpenPositionFound).

## Supported contracts
- **QT-RECOVERY-POS-001**: Confirmed for the tested environment.
- **QT-RECOVERY-POS-002**: Confirmed for the tested run.
- **QT-RECOVERY-DUP-001**: Inconclusive (Absence of duplicates in this run).

## Result and next action
**Result**: Passed
**Evidence quality**: High
**Next action**: ManualCleanupRequired
