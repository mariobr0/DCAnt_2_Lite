# Quantower Contract Test Run: CancelAndObserve (Success)

## Environment
- Scenario: CancelAndObserve
- RunId: run_260724_112811_3cff
- TargetMarker: qtct_240726_8ea09155
- Quantower version: v1.146.16
- Connector: Trading Simulator
- Account mode: Unknown
- Symbol name: Unknown
- Symbol ID: 6470df5d-045f-4c2b-a6e7-a816a265f111
- Note: В данном connector наблюдаемые Account.Id и Account.Name были одинаковыми либо диагностическая стратегия вывела одинаковые значения. Это не исследовалось отдельно.
- Git commit: 8c1b9f (approximate for cancel fix)

## Result

Passed

## Evidence quality

High

## Final state

- Final order state: Cancelled
- Final active matching orders: 0
- Final position state: None
- Manual cleanup required: No

## Observations

- `OrderRemoved` carried `Status=Opened`.
- `OrdersHistoryAdded` carried `Status=Cancelled`.

### Timeline

| Event | Time offset | Key observations |
|---|---|---|
| `StartSnapshot` | `+0 ms` | Active test order found (`fb9c4e13-2481-409d-a623-23c90380e279`), `Status=Opened` |
| `CancelApiResult` | `+8.4 ms` | API returned synchronously, `Status=Success`, `ElapsedMs=0`. |
| `AfterApiCall` | `+8.7 ms` | The order remained in `Core.Orders` with `Status=Opened`. |
| `OrderRemoved` | `+72.0 ms` | Order removed from active orders. `OrderRemoved` carried `Status=Opened`. |
| `OrdersHistoryAdded` | `+73.4 ms` | History added. `OrdersHistoryAdded` carried `Status=Cancelled`. |
| `ObservationTimeout`| `+30s` | Test safely exited. `ActiveTestOrdersCount=0`. |

## Contracts Validated

- QT-CANCEL-001
- QT-CANCEL-002
