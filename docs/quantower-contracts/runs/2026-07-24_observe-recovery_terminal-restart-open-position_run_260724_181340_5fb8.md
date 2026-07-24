# Observe Recovery - Terminal Restart with Open Position (Binance)

**Scenario:** ObserveRecovery (Scenario E)
**RunId:** run_260724_181340_5fb8
**Quantower version:** Unknown
**Connector:** Binance USDT-M Futures
**Account mode:** Live
**Symbol:** ESPORTSUSDT
**Git commit:** Unknown

## Environment
Targeting Binance USDT-M Futures via Quantower.
`ExpectedPositionQuantity` and `ExpectedPositionOpenPrice` were not explicitly provided via UI parameters (left as Unknown), but the recovered position values were extracted via the initial `StartSnapshot` after a full terminal restart.

## Validation
- `RunId` is consistent (`run_260724_181340_5fb8`).
- `Scenario` matches `ObserveRecovery`.
- Timestamps are UTC.
- Initial snapshot successfully captured the position.

## Timeline
1. 18:13:40.079 `StartSnapshot` recorded an existing open position (`PositionState=Open`, `PositionQty=200`, `PositionOpenPrice=0.0458`).
2. 18:14:11.991 `OnStop` was requested by the user.

Real interval between snapshot and stop: ~31 seconds.
No position callbacks (`PositionAdded`, `PositionUpdated`) and no order callbacks were observed during this period.

## ID and Comment correlation
- `OrderId` was not tracked via UI. The snapshot shows `OrderId="Unknown"`, `Status="Unknown"`, `Comment="Unknown"`, as it only read the position from `Core.Positions`.

## Confirmed observations
- After a complete Quantower restart, the active open position was successfully loaded from the broker and populated in `Core.Positions` before/when the diagnostic strategy started.
- The `PositionQty` (200) and `PositionOpenPrice` (0.0458) matched the parameters of the position from previous tests.
- No duplicate or repeated position callbacks occurred for the existing position during the observed 31 seconds.

## Unknowns
- `ExpectedPositionQuantityMatches` and `ExpectedPositionOpenPriceMatches` were `Unknown` due to lack of input parameters, though the raw values match exactly.

## Diagnostic issues
No diagnostic defects affecting this run were observed.

## Final state
- Position is still open.
- Order is filled (from previous run).
- ManualCleanupRequired=Yes (OpenPositionFound).

## Supported contracts
- **QT-TERMINAL-RESTART-POS-001**: Confirmed for the tested environment.
- **QT-RECOVERY-DUP-001**: Inconclusive (Absence of duplicates in this run).

## Result and next action
**Result**: Passed
**Evidence quality**: High
**Next action**: ManualCleanupRequired
