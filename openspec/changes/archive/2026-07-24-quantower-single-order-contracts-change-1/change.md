# Создание базовой диагностики и проверка Place/Cancel (Change 1)

## Цель
Создать диагностическую стратегию для наблюдения размещения и отмены одного limit-order в Quantower. Доказать работоспособность базового сценария: `Place → Observe → Cancel`.

## Требуемое поведение

- **ObserveOnly**:
  - проверить Account и Symbol
  - создать RunId
  - использовать TargetMarker, если он указан
  - подписаться на подтвержденные события
  - записать стартовый snapshot
  - запустить one-shot timeout
  - только наблюдать (ни одного торгового API-вызова)

- **PlaceAndObserve**:
  - проверить параметры
  - подтвердить отсутствие позиции (строгая проверка)
  - убедиться, что активных `qtct_`-ордеров нет
  - создать новый TargetMarker и записать его крупно: `TARGET_MARKER_CREATED=...`
  - записать snapshot до Place
  - вызвать PlaceOrder один раз
  - записать API result
  - записать snapshot после API result
  - наблюдать

- **CancelAndObserve**:
  - проверить TargetMarker
  - найти активные ордера Account + Symbol, отфильтровать по точному Comment
  - найдено 0 или >1: Cancel не выполнять
  - найден ровно 1: записать snapshot, вызвать CancelOrder один раз, записать API result, наблюдать

## Обязательные правила

* `RunId` (сессия лога) и `TargetMarker` (комментарий ордера) — разные значения.
* Формат маркера: `qtct_<date>_<short-guid>` (например, `qtct_260724_a1b2c3d4`). Маркер нельзя обрезать, менять регистр или угадывать заново.
* Если тестовый ордер случайно исполнился, автоматическое закрытие не выполняется; фиксируется состояние, тест помечается `Inconclusive`, требуется ручной cleanup.
* API success не считается broker confirmation.
* Timeout реализуется одним one-shot timer (`System.Threading.Timer`). `_observationFinished` и `_stopping` являются private volatile fields. При срабатывании timeout выставляется `_observationFinished = true;`, записывается финальный snapshot и сообщение. `Stop()` стратегии не вызывается. Последующие callbacks пишутся с пометкой `AFTER_OBSERVATION_TIMEOUT`. (Защита от двойного timeout: `if(_observationFinished) return; _observationFinished = true;`). Флаг защищает от повторной записи при ошибочном вызове, но не используется как сложная машина состояний.
* `OnStop` не выполняет автоматический cleanup. Порядок: 1. `_stopping = true`, 2. остановить и Dispose timer, 3. отписаться от событий, 4. собрать финальный snapshot, 5. записать финальный snapshot, 6. записать итог cleanup (`Manual cleanup required: Yes/No/Unknown`), 7. завершить логирование. Флаг `_stopping` блокирует только внешние callbacks и timeout callback. Он не запрещает `OnStop` непосредственно записать финальный snapshot и итоговую строку.
* Проверка позиции (Place): 
  - нет matching position → zero (если подтверждается API).
  - есть matching position и Quantity == 0 → zero.
  - есть matching position и Quantity != 0 → Place запрещен (учитывать абсолютное значение, не `<= 0`).
  - состояние нельзя прочитать → Place запрещен.
* Запись в файл синхронизируется через простой `private readonly object _logLock = new();`. Формирование строки `BuildSnapshotLine()` и вызовы API происходят строго **вне** lock. Под локом — только `File.AppendAllText`.
* События ничего не изменяют в `TradeCycle`. EngineLoop, SQLite и Outbox не используются. Автоматические повторы (Retry) отсутствуют.
* События сначала изучаются в фактически установленном API, затем подписываются только на существующие.

## Ограничения архитектуры (Что НЕ нужно создавать)

Вся логика помещается в `SingleOrderContractStrategy` (приватные методы `ValidateInputs`, `FindActiveTestOrders`, `ReadPosition`, `ExecutePlaceScenario` и т.д.).
**НЕ ДОБАВЛЯТЬ:**
- `ContractTestState`, `DiagnosticOrder`, `DiagnosticSession`.
- `IContractLogger`, `IOrderFinder`, `SnapshotService`.
- Отдельный dispatcher, EngineLoop, отдельный поток логирования, JSON-модель события, CSV writer.
- Автоматический выбор limit-price, автоматическую остановку Strategy, автоматический cleanup.
- Базовые классы Strategy.

## Логи и Snapshot

Каждая запись должна быть однострочной и содержать:
`UTC timestamp`, `RunId`, `TargetMarker`, `Scenario`, `Source`, `Event`, `AccountId`, `SymbolId`, `OrderId`, `Status`, `Comment`, `Price`, `TotalQuantity`, `FilledQuantity`, `AverageFillPrice`, `PositionQuantity`, `PositionOpenPrice`, `ThreadId`. (Недоступное поле: `Unknown`).

Записывается snapshot: на старте; до команды; после API result; внутри order callback; на timeout; при остановке (`OnStop`).

## Контракты

Документировать в `docs/quantower-contracts/single-order-contracts.md` только:
- **QT-PLACE-001**: Что означает локальный PlaceOrder result?
- **QT-PLACE-002**: Когда ордер появляется в Core.Orders?
- **QT-ID-001**: Какой ID наблюдается у ордера?
- **QT-ID-002**: Сохраняется ли Comment?
- **QT-CANCEL-001**: Что подтверждает отмену?
- **QT-CANCEL-002**: Может ли состояние измениться после Cancel request?

## Не входит (Перенесено во второй change)

- Периодический snapshot.
- Partial fill, полное исполнение.
- Reconnect.
- Restart Strategy, restart терминала.
- PnL-поля как обязательные.
- Классификация duplicate executions.
- Обязательный поиск trade/execution stream.

## Задачи

### Технические задачи
- [x] Уточнить фактически доступные события и статусы Quantower.
- [x] В ObserveOnly запретить любые торговые API-вызовы.
- [x] Реализовать one-shot timeout timer (с `volatile bool _observationFinished`).
- [x] Генерировать TargetMarker только в PlaceAndObserve. Обязательно писать его в лог (`TARGET_MARKER_CREATED=qtct_...`).
- [x] Требовать TargetMarker в CancelAndObserve. Искать только активные matching orders.
- [x] Не выполнять Cancel при нуле или нескольких совпадениях.
- [x] Синхронизировать файловое логирование простым lock (только запись строки в файл).
- [x] Реализовать `volatile bool _stopping` в `OnStop`, отписаться от событий и остановить timer. Не выполнять автоматический cleanup.
- [x] Записать финальное состояние ордера и позиции в OnStop.
- [x] Добавить xUnit-тесты только при наличии естественных чистых функций (например, логики форматирования или генерации). Не создавать искусственных классов ради тестов.

### Ручная проверка
- [x] Запустить ObserveOnly и убедиться, что торговые команды не отправляются.
- [x] Запустить PlaceAndObserve с неисполняемой ценой.
- [x] Скопировать TARGET_MARKER_CREATED из журнала.
- [x] Убедиться, что ордер появился в Quantower.
- [x] Остановить диагностическую Strategy.
- [x] Запустить CancelAndObserve с сохраненным TargetMarker.
- [x] Убедиться, что отправлен ровно один Cancel.
- [x] Проверить итоговое состояние ордера вручную.
- [x] Проверить отсутствие открытой позиции.
- [x] Проверить, остался ли активный test-order.

### Финальная проверка
- [x] Запустить применимые автоматические тесты (Core-тесты должны проходить).
- [x] Выполнить `git diff --check`.
- [x] Убедиться, что EngineLoop и TradeCycle не изменены.
- [x] Убедиться, что не добавлены SQLite, Outbox и QuantowerAdapter.
- [x] Запустить Release-сборку.

## Definition of Done

Change завершен, если:
1. Создана одна диагностическая стратегия (без интерфейсов `IContractLogger`, `IOrderFinder`, `DiagnosticSession` и т.д.). Вся логика в приватных методах `SingleOrderContractStrategy`.
2. `ObserveOnly` не выполняет торговых операций.
3. `PlaceAndObserve` создает не более одного test-order.
4. Перед Place подтверждается строго нулевая позиция (иначе запрет, Quantity учитывается по модулю) и отсутствие активного `qtct_`-ордера для выбранных Account.Id и Symbol.Id.
5. Новый `TargetMarker` создается только в `PlaceAndObserve`. Формат маркера жестко зафиксирован.
6. API result журналируется отдельно от broker state.
7. `CancelAndObserve` требует `TargetMarker`.
8. Cancel выполняется только при одном активном совпадении.
9. Timeout только завершает период наблюдения (one-shot timer), помечая последующие события `AFTER_OBSERVATION_TIMEOUT`, использует private `volatile` флаг защиты.
10. Все временные метки записываются в UTC.
11. Callbacks и snapshots записываются без смешивания строк, логика вынесена из-под `lock`.
12. `OnStop` устанавливает флаг `_stopping` (блокирующий только внешние callbacks, но не саму запись итога), отписывается от событий и не выполняет торговый cleanup.
13. В конце явно указаны matching orders, position quantity и необходимость ручного cleanup.
14. Place и Cancel проверены вручную на демо-счете.
15. Контракты зафиксированы как `Confirmed`, `Rejected` или `Inconclusive`.
16. `TradeCycle`, EngineLoop, SQLite и Outbox не изменены.
17. Release-сборка проходит.
