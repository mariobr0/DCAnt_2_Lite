# ObserveRecovery: Reconnect with Active Order (Binance)

**Scenario:** ObserveRecovery (Reconnect)
**RunId:** run_260724_172730_7709
**Environment:** Binance USDT-M Futures
**Account type:** Live
**Connector-specific:** Yes
**EvidencePseudonymized:** True
**PseudonymizedFields:** AccountName,ConnectionId

*Raw evidence was pseudonymized before repository storage.*

## Result
**Result:** Passed
**EvidenceQuality:** High
**Covered Contracts:** QT-CONNECTION-001, QT-CONNECTION-002, QT-CONNECTION-003, QT-RECOVERY-FILL-001

> Результат подтвержден для использованного подключения Binance USDT-M Futures и протестированной версии Quantower. Результат не переносится автоматически на другие connectors.

## Observations
- Order before reconnect: Opened
- Order after reconnect: Opened
- OrderId preserved: Yes
- Comment preserved: Yes
- Position remained zero: Yes
- Repeated callback observed: Yes (RepeatedStateNotification)
- Order cancellation observed: No
- Order disappearance observed: No
- Manual cleanup required: Yes

## Chronology and Facts
1. **StartSnapshot**: Order was present before reconnect.
2. **Reconnect via "Connecting"**: Quantower sent an `OrderEvent` during the `Connecting` state. The order was re-delivered and properly classified as `RepeatedStateNotification`.
3. **Speed to Connected**: В данном запуске connection уже наблюдался как `Connected` на первом probe через 100 мс после события, зарегистрированного при состоянии `Connecting`.
4. **Order Persisted**: В представленных snapshots и probes временное отсутствие ордера не наблюдалось. Во время reconnect пришел повторный `OrderEvent`.
5. **Position**: Осталась нулевой.

## Contract Statuses
- **QT-CONNECTION-001**: Confirmed for the tested run. Во время reconnect было наблюдено состояние connection `Connecting`. В этот момент существующий ордер оставался доступен в active collection со статусом `Opened`. На первом probe через 100 мс connection уже наблюдался как `Connected`, а ордер оставался открытым. *(Ограничение: Тест не доказывает, что ордер никогда кратковременно не исчезает между snapshots).*
- **QT-CONNECTION-002**: Confirmed for the tested run. ID, Comment, Price, Quantity, Status сохранились.
- **QT-CONNECTION-003**: Confirmed. После reconnect-related transition Quantower может повторно доставить уже известное состояние существующего ордера. Такое событие нельзя использовать как основание повторно создавать локальный ордер или повторять финансовое действие.
- **QT-RECOVERY-FILL-001**: Confirmed в узком смысле (Repeated open state recognized. Fill repeat не тестировался).

## Limitations
- Expected Price и Quantity не были переданы в `ObserveRecovery`, поэтому строгая equality проверка не была пройдена автоматически (хотя данные сохранились, что видно из подготовки).
