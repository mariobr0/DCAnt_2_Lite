# Verify Quantower Fill Contracts

## Цель

Установить фактическое поведение Quantower при полном и частичном
исполнении одного ордера.

Определить семантику order events, FilledQuantity,
AverageFillPrice и position snapshot.

Не создавать production-модель Execution до подтверждения
доступного источника отдельных сделок.

## Scope

Проверяется:

- один Account;
- один Symbol;
- один тестовый ордер;
- одно полное исполнение;
- попытка получить partial fill;
- order callbacks;
- order history callbacks;
- position snapshot;
- отдельный Trade/Execution source, если он доступен.

Reconnect и restart в этот change не входят.

## Обязательные правила

- Не более одного test-order одновременно.
- Перед тестом позиция должна быть подтвержденно нулевой.
- `PlaceForFullFill` и `PlaceForPartialFill` всегда создают новый TargetMarker.
- `ObserveFill` никогда не создает новый TargetMarker.
- TargetMarker для `ObserveFill` необязателен.
- Если TargetMarker указан, стратегия дополнительно ищет связанный active order и журналирует совпадения.
- Если TargetMarker не указан, стратегия наблюдает состояние выбранных Account и Symbol без выводов об ownership конкретного ордера.
- RunId создается заново при каждом запуске.
- API result отделяется от order state и position state.
- OrdersHistoryAdded не считается Execution.
- FilledQuantity не считается delta или cumulative без evidence.
- AverageFillPrice не считается ценой последнего fill без evidence.
- Синтетический ExecutionId запрещен.
- Duplicate order state не считается duplicate execution.
- Автоматический повтор Place запрещен.
- Автоматический новый торговый цикл запрещен.
- После теста cleanup выполняется вручную и проверяется отдельно.

## Сценарии

### ObserveFill

Ничего не размещает.

Наблюдает указанный TargetMarker и журналирует:

- active order;
- order callbacks;
- order history callbacks;
- position snapshot;
- доступные trade/execution entities.

### PlaceForFullFill

Размещает один небольшой исполнимый Limit order.

После Place только наблюдает.

Не выставляет TP, SL или exit-order автоматически.

### PlaceForPartialFill

Размещает один Limit order с параметрами, выбранными пользователем
для попытки получить partial fill.

Если partial fill не возник, результат помечается Inconclusive.

### Завершение full-fill сценария

После полного исполнения диагностическая Strategy не закрывает позицию
автоматически.

Порядок:

1. Дождаться терминального состояния ордера и position snapshots.
2. Остановить диагностическую Strategy.
3. Сохранить raw evidence full-fill запуска.
4. Обработать запуск через `collect-contract-test`.
5. Закрыть позицию вручную через Quantower.
6. Запустить `ObserveFill` без TargetMarker или с marker завершенного
   ордера.
7. Подтвердить:
   - ActiveTestOrdersCount = 0;
   - PositionState = None;
   - PositionQuantity = 0;
   - ManualCleanupRequired = No.
8. Сохранить cleanup run отдельно.

### Завершение partial-fill сценария

После наблюдения partial fill диагностическая Strategy не отменяет остаток автоматически.

Порядок:

1. Сохранить raw evidence partial-fill запуска.
2. Остановить диагностическую Strategy.
3. Проверить фактический остаток ордера и позицию.
4. Отменить остаток вручную, если он еще активен.
5. Закрыть позицию вручную.
6. Запустить ObserveFill и подтвердить:
   - ActiveTestOrdersCount = 0;
   - PositionState = None;
   - PositionQuantity = 0.

## Что журналировать

Для каждого события (включая TARGET_MARKER_CREATED, API result, ошибки, probes, timeout, OnStop):

- Sequence;
- ObservedAtUtc;
- ProviderTime;
- RunId;
- TargetMarker;
- Source;
- Event;
- AccountId;
- SymbolId;
- OrderId;
- Status;
- Comment;
- Price;
- TotalQuantity;
- FilledQuantity;
- AverageFillPrice;
- PositionState;
- PositionQuantity;
- PositionOpenPrice;
- ThreadId.

Правила для Sequence и времени:
- Sequence монотонно увеличивается для каждой строки текущего RunId.
- Sequence отражает порядок записи в диагностический лог.
- Sequence не считается sequence broker или connector.
- ObservedAtUtc является временем наблюдения события стратегией.
- ProviderTime записывается отдельно, только если оно доступно.

Для анализа FilledQuantity:
- PreviousFilledQty;
- CurrentFilledQty;
- ObservedDifference.

Последнее наблюдаемое `FilledQuantity` хранится отдельно для каждого `OrderId`.

При первом событии ордера:
PreviousFilledQty=Unknown
CurrentFilledQty=<value>
ObservedDifference=Unknown

При последующих событиях:
ObservedDifference = CurrentFilledQty - PreviousFilledQty

Значение PreviousFilledQty обновляется после журналирования каждого валидного callback данного OrderId, включая ноль.

ObservedDifference=0 означает только отсутствие изменения между двумя наблюдаемыми snapshots. Это не является отдельным исполнением нулевого объема.

ObservedDifference является только диагностической арифметикой.
Он не называется ExecutionQuantity и не считается отдельным fill,
пока это не подтверждено отдельным trade source.

Если существует отдельная сделка:

- доступный Trade/Execution ID;
- Price;
- Quantity;
- Side;
- Time;
- OrderId.

Недоступное значение записывать как Unknown.

## Snapshot

Записывать:

- перед Place;
- сразу после API result;
- при каждом callback;
- Набор fill-probes запускается, если:
  - CurrentFilledQty доступен;
  - CurrentFilledQty > 0;
  - значение отличается от последнего наблюдаемого FilledQty данного OrderId.
  
  Порядок probes:
  - snapshot внутри callback;
  - через 100 мс;
  - через 250 мс;
  - через 500 мс;
  - через 1000 мс.

  Callback с FilledQty=0 не запускает fill-probes.
  Callback с тем же FilledQty повторно probes не запускает.
  Probes относятся к конкретному значению FilledQty и должны записывать: TriggerFilledQty, ProbeDelayMs, PositionQty, PositionOpenPrice.
- на timeout;
- в OnStop.

## Проверяемые контракты

### QT-FILL-001

Является ли FilledQuantity cumulative или delta?
- Full-fill test сам по себе не подтверждает cumulative/delta семантику FilledQuantity.
- Если partial fill не воспроизведен и отдельный trade source недоступен, QT-FILL-001 остается Inconclusive.

### QT-FILL-002

Что означает AverageFillPrice?
- Аналогично, один fill не доказывает семантику AverageFillPrice.

### QT-FILL-003

Существует ли отдельный источник Trade/Execution?
- `NotSupported` используется только при подтвержденном отсутствии источника в API или connector.
- Если источник существует, но событие не наблюдалось, используется `Inconclusive`.

### QT-FILL-004

Есть ли стабильный ID отдельного исполнения?

### QT-FILL-005

Каков порядок order callbacks при исполнении?

### QT-POS-001

Когда изменяется Position.Quantity относительно order events?

### QT-POS-002

Что означает Position.OpenPrice после одного или нескольких fills?

### QT-DUP-001

Повторяются ли одинаковые order state events?
Проверяются отдельно:
1. Multi-source notification: одинаковое состояние одного OrderId наблюдается через разные Source.
2. Exact same-source duplicate: один Source повторяет событие с одинаковыми OrderId, Status, FilledQuantity, AverageFillPrice, Comment.
Если exact duplicate не наблюдался, результат записывается как `NotObservedInThisRun`, а не как доказательство невозможности дублей.

## Не входит

- reconnect;
- restart Strategy;
- restart Quantower;
- SQLite;
- Outbox;
- QuantowerAdapter;
- TradeCycle;
- TP;
- SL;
- DCA;
- automatic retry;
- automatic cleanup;
- realized PnL;
- комиссии.

## Задачи

### Подготовка
- [x] Изучить доступные trade/execution APIs установленной версии.
- [x] Добавить строгий preflight позиции и активных test-orders.
- [x] Добавить Sequence во все строки журнала.
- [x] Добавить PreviousFilledQty, CurrentFilledQty и ObservedDifference по OrderId.
- [x] Добавить position snapshot внутри каждого order callback.
- [x] Добавить ограниченные probes после изменения FilledQty.
- [x] Добавить логирование отдельной сделки, если источник существует.
- [x] Разделить multi-source notifications и exact duplicates.

### Техническая проверка
- [x] Выполнить Release build.
- [x] Выполнить применимые автоматические тесты.
- [x] Выполнить git diff --check.
- [x] Провести строгий review.
- [x] Убедиться, что TradeCycle, EngineLoop, SQLite и Outbox не изменены.

### Ручные тесты
- [x] Провести full-fill test.
- [x] Сохранить full-fill evidence до cleanup.
- [x] Выполнить ручной cleanup.
- [x] Подтвердить cleanup через ObserveFill.
- [x] Попытаться провести partial-fill test.
- [x] Сохранить partial evidence до cleanup.
- [x] Выполнить ручной cleanup partial-сценария.
- [x] Подтвердить cleanup через ObserveFill.

### Документирование
- [x] Обработать каждый запуск через collect-contract-test.
- [x] Обновить single-order-contracts.md.
- [x] Зафиксировать Inconclusive и NotSupported без предположений.

## Definition of Done

1. Полное исполнение одного ордера зафиксировано.
2. API result отделен от order callbacks.
3. Order state отделен от отдельного Execution.
4. FilledQuantity не интерпретируется без evidence.
5. AverageFillPrice не интерпретируется без evidence.
6. Position snapshots записаны рядом с fill events.
7. Separate Trade/Execution source имеет подтвержденный статус: Confirmed, NotSupported или Inconclusive.
8. ExecutionId не создается синтетически.
9. Partial fill либо исследован, либо честно отмечен Inconclusive.
10. Duplicate order states не объявлены duplicate executions.
11. После тестов активных test-orders нет.
12. После cleanup позиция равна нулю.
13. TradeCycle, EngineLoop, SQLite и Outbox не изменены.
14. Release build и автоматические тесты проходят.
15. Каждая журнальная запись имеет монотонный Sequence.
16. Unknown position state никогда не трактуется как zero.
17. ObservedDifference не называется ExecutionQuantity до подтверждения семантики.
18. Full-fill evidence не смешано с ручным закрытием позиции.
19. Multi-source notifications отделены от exact duplicate callbacks.
20. Ручной cleanup подтвержден отдельным snapshot или ObserveFill run.
21. Place-сценарии создают новый TargetMarker, а ObserveFill использует переданный marker существующего ордера.
22. QT-FILL-001 и QT-FILL-002 не получают статус Confirmed только на основании одного полного fill.
23. Fill-probes создаются только для нового ненулевого значения FilledQuantity и не повторяются для неизменившегося значения.
24. NotSupported для Trade/Execution source используется только при подтвержденном отсутствии поддержки.
