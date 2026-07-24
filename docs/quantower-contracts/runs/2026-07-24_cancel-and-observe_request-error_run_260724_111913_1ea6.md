# Quantower Contract Test Run: CancelAndObserve (Request Error)

## Environment
- Scenario: CancelAndObserve
- RunId: run_260724_111913_1ea6
- TargetMarker: qtct_240726_8ea09155
- Quantower version: v1.146.16
- Connector: Unknown
- Account mode: Unknown
- Symbol name: Unknown
- Symbol ID: 6470df5d-045f-4c2b-a6e7-a816a265f111
- Note: В данном connector наблюдаемые Account.Id и Account.Name были одинаковыми либо диагностическая стратегия вывела одинаковые значения. Это не исследовалось отдельно.
- Git commit: 2c30f86

## Result

MustBeRepeated

## Diagnostic issue

`Core.CancelOrder(CancelOrderRequestParameters)` threw `NullReferenceException` while resolving `ConnectionId`.

The external cancel lifecycle was not tested.

## Final state

- Matching active orders: 1
- Order status: Opened
- Position state: None
- Manual cleanup required: Yes

## Contracts

- QT-CANCEL-001: NotTested
- QT-CANCEL-002: NotTested
