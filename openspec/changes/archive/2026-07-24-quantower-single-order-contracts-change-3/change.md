# Verify Quantower Recovery Contracts

## Цель

Установить фактическое поведение Quantower при:

* остановке и повторном запуске диагностической Strategy;
* reconnect торгового соединения;
* полном перезапуске Quantower;
* восстановлении активного ордера;
* восстановлении открытой позиции;
* повторной доставке order/history/position events.

Главная задача Change 3:

> Понять, какие данные после восстановления являются текущими snapshots, какие callbacks повторяются, а какие события действительно являются новыми.

До завершения этих проверок запрещено реализовывать production recovery, reconciliation и повторную отправку торговых команд.

Quantower предоставляет данные отдельных подключений через `Connection`, включая связанные business objects. Поэтому результаты восстановления должны быть ограничены конкретной версией Quantower и конкретным connector.

## Scope

Проверяются пять изолированных сценариев:

1. Restart диагностической Strategy при активном ордере.
2. Reconnect connector при активном ордере.
3. Restart Quantower при активном ордере.
4. Restart диагностической Strategy при открытой позиции.
5. Restart Quantower при открытой позиции.

Для каждого сценария используется один Account и один Symbol.

Для active-order сценариев используется один сохраненный TargetMarker и один ранее наблюдавшийся OrderId.

Для position-сценариев используются сохраненные PositionQuantity и PositionOpenPrice. Entry OrderId записывается как справочная корреляция, но активный ордер не требуется.

Сценарии выполняются отдельно. Нельзя одновременно проверять active order и open position, если это делает результат неоднозначным.

Каждый из пяти recovery-сценариев начинается с отдельной подготовки и заканчивается отдельным cleanup.
Нельзя использовать один test-order как evidence сразу для нескольких recovery-сценариев.

Для каждого active-order сценария:
PrepareActiveOrder → recovery action → сохранить evidence → ручной Cancel → ObserveRecovery cleanup

Для каждого position-сценария:
PrepareOpenPosition → recovery action → сохранить evidence → ручное закрытие позиции → ObserveRecovery cleanup

## Обязательные правила

* Сценарии, не требующие сохранения внешнего состояния, выполняются на Trading Simulator или demo account.
* Recovery-сценарии, требующие сохранения ордера или позиции после reconnect или restart, выполняются на подключении (включая Live), которое действительно хранит это состояние вне процесса Quantower. Trading Simulator для этого не подходит, если он сбрасывает состояние при остановке.
* Перед каждым сценарием фиксируется исходное состояние.
* `RunId` создается заново при каждом запуске Strategy.
* `TargetMarker` после restart не генерируется заново.
* `TargetMarker` передается пользователем из предыдущего запуска.
* Ранее наблюдавшийся `OrderId` передается отдельно как ожидаемое диагностическое значение.
* `Comment` не является единственным доказательством ownership.
* Корреляция использует Account, Symbol, TargetMarker и ранее наблюдавшийся OrderId.
* Совпадение только цены и quantity не доказывает ownership.
* Callback после restart или reconnect не считается новым финансовым событием только потому, что он пришел после запуска Strategy.
* `OrdersHistoryAdded` не считается Execution.
* Повторный `Filled` не считается новым исполнением.
* Восстановленная Position не прибавляется к старому локальному значению.
* Snapshot Position трактуется как абсолютное текущее состояние.
* Временное отсутствие ордера во время reconnect не считается Cancel.
* `PrepareActiveOrder` и `PrepareOpenPosition` могут выполнить ровно один PlaceOrder после успешного preflight.
* Повторный PlaceOrder, retry PlaceOrder и дополнительный PlaceOrder после timeout запрещены.
* `ObserveRecovery` никогда не выполняет Place, Cancel, Exit или retry.
* Автоматический Cancel и автоматический Exit запрещены во всех режимах.
* При неизвестном состоянии новые торговые команды запрещены.
* Cleanup выполняется вручную после сохранения evidence.
* `TradeCycle`, EngineLoop, SQLite и Outbox не изменяются.

## Диагностические режимы

### PrepareActiveOrder

- Проверяет отсутствие позиции и test-orders.
- Создает новый TargetMarker.
- Размещает один неисполняемый Limit-order.
- Записывает TargetMarker и OrderId.
- Не отменяет ордер автоматически.

### PrepareOpenPosition

- Проверяет чистое исходное состояние.
- Создает новый TargetMarker.
- Размещает один небольшой исполнимый Limit-order.
- Дожидается позиции.
- Записывает Quantity и OpenPrice.
- Не закрывает позицию автоматически.

### ObserveRecovery

- Не выполняет торговых операций.
- Не генерирует TargetMarker.
- Принимает необязательные параметры:
  - TargetMarker;
  - ExpectedOrderId;
  - ExpectedOrderPrice;
  - ExpectedOrderTotalQuantity;
  - ExpectedPositionQuantity;
  - ExpectedPositionOpenPrice.
- Для active-order сценариев используются TargetMarker, ExpectedOrderId, ExpectedOrderPrice и ExpectedOrderTotalQuantity.
- Для position-сценариев используются ExpectedPositionQuantity и ExpectedPositionOpenPrice.
- Наблюдает текущие ордера, позицию, connection state и callbacks.

## Recovery baseline и last observed state

В начале `ObserveRecovery` сохраняется recovery baseline:
- BaselineOrderId;
- BaselineOrderStatus;
- BaselineFilledQty;
- BaselineComment;
- BaselinePositionQty;
- BaselinePositionOpenPrice.

Дополнительно в памяти хранится последнее наблюдаемое состояние:
- LastOrderId;
- LastOrderStatus;
- LastFilledQty;
- LastComment;
- LastPositionQty;
- LastPositionOpenPrice.

Классификация выполняется так:
- `CurrentSnapshot`: состояние прочитано при старте;
- `RepeatedStateNotification`: callback совпадает с последним наблюдаемым состоянием;
- `NewStateChange`: callback отличается от последнего наблюдаемого состояния;
- `Unknown`: данных недостаточно.

После классификации валидное наблюдаемое состояние становится новым last observed state.
Baseline остается неизменным и используется для сравнения состояния до и после recovery.

Новый Sequence или timestamp сам по себе не делает callback новым финансовым событием.
`NewStateChange` используется только если наблюдаемое значение отличается от последнего наблюдаемого состояния.

Временное исчезновение active order во время reconnect записывается как изменение наблюдаемой доступности, но не классифицируется как Cancelled без отдельного evidence. Изменение `OrderPresentInActiveCollection` во время reconnect отражает только наблюдаемую доступность объекта. `False` не означает `Cancelled`, если терминальный статус Cancelled не подтвержден отдельным evidence.

## Журналирование

Каждая строка содержит:

- Sequence;
- ObservedAtUtc;
- ProviderTime;
- RunId;
- Scenario;
- RecoveryPhase;
- ConnectionId;
- ConnectionState;
- AccountId;
- SymbolId;
- TargetMarker;
- ExpectedOrderId;
- ExpectedOrderPrice;
- ExpectedOrderTotalQuantity;
- ExpectedPositionQuantity;
- ExpectedPositionOpenPrice;
- ExpectedOrderIdMatches;
- ExpectedOrderPriceMatches;
- ExpectedOrderTotalQuantityMatches;
- ExpectedPositionQuantityMatches;
- ExpectedPositionOpenPriceMatches;
- Source;
- Event;
- OrderId;
- OrderPrice;
- OrderTotalQuantity;
- Status;
- Comment;
- FilledQuantity;
- OrderPresentInActiveCollection;
- PositionState;
- PositionQuantity;
- PositionOpenPrice;
- Classification;
- ThreadId.

Недоступные значения записываются как `Unknown`.
Пустая строка не заменяет `Unknown`.

Sequence отражает порядок строк диагностического лога, но не является sequence broker или connector.

## Проверяемые контракты

### QT-STRATEGY-RESTART-001
Виден ли active order сразу после restart Strategy?

### QT-STRATEGY-RESTART-002
Повторяются ли callbacks существующего ордера?

### QT-CONNECTION-001
Что происходит с active order во время reconnect?

### QT-CONNECTION-002
Сохраняются ли OrderId и Comment после reconnect?

### QT-TERMINAL-RESTART-001
Восстанавливается ли active order после restart Quantower?

### QT-TERMINAL-RESTART-002
Сохраняются ли ID, Comment, Price, Quantity и Status?

### QT-RECOVERY-POS-001
Когда Position становится доступна после restart?

### QT-RECOVERY-POS-002
Сохраняются ли PositionQuantity и PositionOpenPrice?

### QT-RECOVERY-DUP-001
Повторяются ли старые order/history/position states?

### QT-RECOVERY-FILL-001
Можно ли отличить восстановленный Filled snapshot от нового fill?

## Задачи

### Подготовка
- [x] Изучить публичные connection events и connection states установленной версии Quantower.
- [x] Запретить Reflection. Использовать только публичный API.
- [x] Добавить режимы PrepareActiveOrder, PrepareOpenPosition и ObserveRecovery.
- [x] Добавить expected order и position parameters.
- [x] Добавить recovery baseline и last observed state.
- [x] Добавить Classification.
- [x] Добавить OrderPresentInActiveCollection.
- [x] Добавить OrderPrice и OrderTotalQuantity.
- [x] Добавить recovery probes после изменения connection state.
- [x] Проверить, что каждый сценарий имеет отдельную подготовку и отдельный cleanup.
- [x] Если публичный connection callback отсутствует, фиксировать состояние через доступный connection snapshot и recovery probes.
- [x] Не объявлять временное отсутствие order доказательством Cancel.

### Техническая проверка
- [x] Выполнить Release build.
- [x] Выполнить все применимые автоматические тесты.
- [x] Выполнить `git diff --check`.
- [x] Провести strict review без автоматического изменения кода.
- [x] Проверить lock ordering.
- [x] Проверить фильтры Account и Symbol.
- [x] Проверить корреляцию по TargetMarker и ExpectedOrderId.
- [x] Убедиться, что диагностическая Strategy не выполняет автоматические торговые операции (кроме одного Place в Prepare режимах).
- [x] Убедиться, что TradeCycle, EngineLoop, SQLite и Outbox не изменены.

### Ручные тесты
- [x] Restart Strategy с активным ордером.
- [x] Reconnect с активным ордером.
- [x] Restart Quantower с активным ордером.
- [x] Restart Strategy с открытой позицией.
- [x] Restart Quantower с открытой позицией.

### Документирование и Cleanup
- [x] Каждый запуск сохранен через `collect-contract-test`.
- [x] Cleanup подтверждается отдельным запуском `ObserveRecovery` без TargetMarker: ActiveTestOrdersCount = 0; PositionState = None; PositionQuantity = 0; ManualCleanupRequired = No.
- [x] Зафиксировать выводы по QT- контрактам.
- [x] Добавлены/обновлены Recovery-контракты в `single-order-contracts.md`.

## Definition of Done

1. Restart Strategy с active order исследован.
2. Reconnect с active order исследован.
3. Restart Quantower с active order исследован.
4. Restart Strategy с open position исследован.
5. Restart Quantower с open position исследован.
6. OrderId и Comment после recovery сравнены с исходными.
7. Price, Quantity и Status после recovery записаны.
8. PositionQuantity и PositionOpenPrice после recovery записаны.
9. Стартовый snapshot отделен от нового callback.
10. Повторный Filled не считается новым execution.
11. Восстановленная позиция не прибавляется к сохраненной quantity.
12. `ObserveRecovery` не выполняет Place, Cancel, Exit или retry. Подготовительные режимы выполняют не более одного PlaceOrder после успешного preflight.
13. Каждый запуск сохранен через `collect-contract-test`.
14. Cleanup active-order сценариев подтвержден.
15. Cleanup position-сценариев подтвержден.
16. Активных test-orders нет.
17. PositionState=None и PositionQuantity=0.
18. Неясные результаты отмечены `Inconclusive`.
19. Production recovery и reconciliation не реализованы.
20. TradeCycle, EngineLoop, SQLite и Outbox не изменены.
21. Release build и тесты проходят.
22. `git diff --check` проходит.
23. Каждый recovery-сценарий использовал отдельный test-order или отдельную test-position.
24. OrderPrice и OrderTotalQuantity после recovery сравнены с исходными значениями.
25. ExpectedPositionQuantity и ExpectedPositionOpenPrice сравнены с восстановленным snapshot.
26. Временное отсутствие order во время reconnect не было интерпретировано как Cancel.
27. Использовались только публичные connection APIs; Reflection отсутствует.
28. Каждый сценарий имеет отдельный raw evidence, run report и cleanup evidence.

## Результат проверки

- Сборка: Успешно (0 warnings, 0 errors)
- Тесты: Успешно (Passed: 111)
- Замечания: Strict review пройден, trailing whitespace устранены, lock ordering в норме.
