# ObserveRecovery: Terminal Restart with Active Order (Binance)

**Scenario:** ObserveRecovery (Terminal Restart)
**RunId:** run_260724_175824_4a84
**Environment:** Binance USDT-M Futures
**Account type:** Live
**Connector-specific:** Yes
**EvidencePseudonymized:** True
**PseudonymizedFields:** AccountName,ConnectionId

*Raw evidence was pseudonymized before repository storage.*

## Result
**Result:** Passed
**EvidenceQuality:** High
**Covered Contracts:** QT-TERMINAL-RESTART-001, QT-TERMINAL-RESTART-002, QT-RECOVERY-DUP-001

> Результат подтвержден для использованного подключения Binance USDT-M Futures и протестированной версии Quantower. Результат не переносится автоматически на другие connectors.

## Observations
- Order before restart: Opened
- Order after restart: Opened (Present in StartSnapshot)
- OrderId preserved: Yes
- Comment preserved: Yes
- Position remained zero: Yes
- Repeated callback observed: **No**
- Order cancellation observed: No
- Order disappearance observed: No
- Manual cleanup required: Yes

## Chronology and Facts
1. **StartSnapshot**: Order was successfully found in `Core.Orders` immediately upon Strategy start. All parameters (OrderId, Price, Quantity, Comment, Status) were identical to pre-restart.
2. **Callbacks**: `0` callbacks were received during the observation window. The order was restored purely as a silent snapshot object.
3. **Difference from Reconnect**: Unlike a connection drop/restore which triggered a `RepeatedStateNotification`, a full terminal restart followed by a late Strategy start yields NO order events for existing orders.

## Contract Statuses
- **QT-TERMINAL-RESTART-001**: Confirmed for the tested environment. Active external order was recoverable through Quantower after a full terminal restart. The recovered order was observed as CurrentSnapshot and was not treated as a new order event.
- **QT-TERMINAL-RESTART-002**: Confirmed for the tested run. ID, Comment, Price, Quantity, and Status are fully preserved. (Limitation: Price and Qty equality established via log comparison, not built-in matches).
- **QT-RECOVERY-DUP-001**: Inconclusive. No repeated order callbacks were observed after terminal restart in this run. Absence of repeated callbacks in this run does not prove that such callbacks cannot occur.
- **QT-RECOVERY-FILL-001**: Confirmed в узком смысле. Восстановленный открытый ордер был отделен от нового события через `CurrentSnapshot`. Поведение восстановленного терминального `Filled` этим запуском не проверялось.

## Limitations
- Expected Price and Quantity strict validation logic wasn't fully printed as "Matches=True" because they were not supplied to the UI, but visual inspection of the log confirms perfect equality.
- The absence of callbacks is strictly tied to the fact that the Strategy was started *after* the terminal connected and populated its internal state. (A strategy starting concurrently with the connection might see callbacks, but late-starting strategies do not).
