# Implement Engine Loop Foundation

## Зачем (Цель изменения)

Создать простой последовательный цикл обработки внутренних сообщений DCAnt.

EngineLoop должен позволять нескольким потокам безопасно добавлять сообщения, но обрабатывать эти сообщения должен ровно один consumer.

Это гарантирует, что торговое состояние бота изменяется строго в одном потоке, исключая состояние гонки при параллельной обработке данных.

## Файловая структура

Новые файлы будут созданы в `src/DCAnt2.Core/Engine/`:
- `EngineLoop.cs`
- `EngineLoopState.cs`
- `EngineMessage.cs`

Новый проект или архитектурный слой создаваться не будет.

## Требуемое поведение

Первая версия делает только это:
1. `Start`
2. → принять сообщение
3. → поставить его в очередь
4. → обработать сообщения по одному
5. → корректно остановиться
6. → при необработанной ошибке перейти в Faulted

Жизненный цикл `EngineLoopState` состоит только из:
- `Created`: Объект создан, но обработка еще не запущена.
- `Running`: Принимает и обрабатывает сообщения.
- `Stopping`: Остановка запрошена, новые сообщения не принимаются.
- `Stopped`: Цикл штатно завершен.
- `Faulted`: Выброшено необработанное исключение, продолжать работу нельзя.

## Публичный интерфейс

```csharp
public EngineLoop(Action<EngineMessage> messageHandler, Action<Exception>? panicHandler = null)
public EngineLoopState State { get; }
public void Start()
public bool TryEnqueue(EngineMessage message)
public Task StopAsync()
public ValueTask DisposeAsync()
```
### Правила `TryEnqueue()`
- `message == null` → бросает `ArgumentNullException`.
- Если `State == Running` → метод пытается записать сообщение (`TryWrite`). Возвращает `true` при успехе, `false` если очередь переполнена.
- Если `State != Running` (состояния `Created`, `Stopping`, `Stopped`, `Faulted`) → метод сразу возвращает `false`. Не накапливать сообщения до `Start()`.

### Правила `Start()`
- `Start()` разрешает только переход `Created → Running`.
- При вызове в любом другом состоянии (`Running`, `Stopping`, `Stopped`, `Faulted`) выбрасывается `InvalidOperationException`.
- Повторный запуск запрещен. Если цикл остановился, для нового запуска создается новый объект `EngineLoop`.

### Последовательная обработка
- В цикле вызывается синхронный `Action<EngineMessage>` (один потребитель).
- Обработчик не должен выполнять долгих операций (ввод/вывод, БД, сетевые запросы).
- `messageHandler` не имеет права вызывать `StopAsync()` или `DisposeAsync()` текущего экземпляра EngineLoop (чтобы избежать взаимной блокировки/deadlock). Остановкой управляет только внешний владелец.
- Успешно принятые сообщения обрабатываются в порядке их записи в Channel. Для одновременных вызовов `TryEnqueue` из разных потоков заранее не определяется, какой вызов будет записан первым. После записи порядок сохраняется.

### Обработка ошибки (Panic Protocol)
Если `messageHandler` выбрасывает исключение:
1. Под `lock` `State` переходит в `Faulted`.
2. Чтение из очереди прекращается, Writer закрывается (новые сообщения не принимаются).
3. Оставшиеся в очереди сообщения НЕ обрабатываются (т.к. состояние считается недостоверным).
4. Вызывается `panicHandler(Exception)` вне `lock`.
5. Если сам `panicHandler` выбрасывает исключение — оно перехватывается (`try/catch`) и игнорируется, чтобы таск потребителя всё равно мог завершиться.
6. Исключение `messageHandler` не игнорируется и не позволяет продолжить обработку очереди. Оно переводит EngineLoop в `Faulted` и передается в `panicHandler`, но повторно из loop task и `StopAsync` не выбрасывается. Состояние `Faulted` является результатом аварийного завершения.

### Штатная остановка (`StopAsync` и `DisposeAsync`)
- Вызов `StopAsync()` переводит `State` в `Stopping`.
- Закрывается `Writer` (`_channel.Writer.TryComplete()`), новые сообщения отклоняются.
- Потребитель дорабатывает уже принятые сообщения до конца. 
- После нормального окончания цикла чтения, `State` переходит в `Stopped` (только если был `Stopping`, состояние `Faulted` не перезаписывается).
- Остановка не использует `CancellationTokenSource` (отсутствует жесткое прерывание).
- Семантика `StopAsync`:
  - `Created` → Writer закрывается, `State` становится `Stopped`, `loop task` не создается, последующий `Start()` запрещен.
  - `Running` → переводит в `Stopping`, закрывает очередь, дожидается завершения `loop task`, затем `Stopped`.
  - `Stopping` → дождаться существующей `loop task`.
  - `Stopped` → мгновенно завершиться (ничего не делать).
  - `Faulted` → дождаться завершения `loop task`, но оставить состояние `Faulted`.
- `EngineLoop` должен реализовывать `IAsyncDisposable`. Метод `DisposeAsync` вызывает `await StopAsync()`.
- Имплементация синхронного `IDisposable` (с `.Wait()`) запрещена, чтобы избежать deadlock'ов.

### Потокобезопасность
- Использовать простой `lock (_stateLock)` для чтения и изменения жизненного цикла. Свойство `State` читает приватное поле `_state` под этим же `lock`.
- `TryEnqueue()` читает `State` внутри `lock` и делает `TryWrite`.
- НЕ использовать сложные lock-free конструкции (`Interlocked.CompareExchange`).
- Строго запрещено удерживать `lock` во время длительных операций: ожидания Task (`await _loopTask`), вызова `messageHandler` или вызова `panicHandler`.

## Детали реализации очереди
- Использовать `Channel<EngineMessage>`.
- Очередь должна быть ограниченной (bounded channel) для защиты от исчерпания памяти.
- Емкость задается внутренней константой: `private const int ChannelCapacity = 1024;`.
- Настройки: `SingleReader = true`, `SingleWriter = false`, `FullMode = BoundedChannelFullMode.Wait`.
- `TryEnqueue()` использует `TryWrite`. Возврат `false` означает, что сообщение не принято и не будет обработано. EngineLoop сам не делает retry и не переходит в Faulted из-за очереди — реакция остается на совести вызывающего кода.

## Не входит

EngineLoop не должен становиться универсальным actor framework. Строго не нужны:
- generic pipeline
- middleware
- dependency injection
- приоритетные очереди
- несколько consumers
- отдельный event bus
- command dispatcher
- reflection
- retry framework
- отдельный scheduler
- сложная telemetry
- persistence
- автоматическое восстановление

Также не входят:
- Любые торговые сообщения (`ExecutionMessage`, `OrderUpdateMessage`, `PositionSnapshotMessage`, `PlaceOrderMessage`, `CancelOrderMessage`). Они появятся после contract tests.
- Реализация Outbox (накопление и отправка команд брокеру).
- Подключение реальных Quantower callbacks (это потребует отдельных contract-тестов).
- Логика принятия торговых решений (размещение сетки в ответ на рыночные данные).

### Что строго не должен делать EngineLoop
EngineLoop вызывает предоставленный handler, но сам **никогда не должен**:
- рассчитывать DCA-сетку;
- изменять `TradeCycle` самостоятельно;
- знать Quantower;
- открывать SQLite;
- отправлять ордера;
- повторять сообщения или выполнять retry;
- делать reconciliation;
- запускать таймеры;
- создавать новый TradeCycle;
- хранить торговые effects;
- различать Entry, TP, SL;
- обрабатывать partial fills;
- логировать в файл.

## Задачи (Порядок реализации)

### Шаг 1: Базовые контракты
- [x] Создать `EngineMessage.cs` (пустой `abstract record`).
- [x] Создать `EngineLoopState.cs` (enum: `Created`, `Running`, `Stopping`, `Stopped`, `Faulted`).
- [x] Собрать проект, убедиться в отсутствии ошибок.

### Шаг 2: Инициализация и Запуск
- [x] Создать `EngineLoop.cs` с конструктором, свойством `State` и bounded channel.
- [x] Реализовать потокобезопасный метод `Start()` и основной асинхронный цикл чтения.
- [x] Добавить базовые lifecycle-тесты (Constructor, null handler, Start, двойной Start, `Start_AfterStopped_ThrowsInvalidOperationException`).

### Шаг 3: Прием сообщений
- [x] Добавить `TryEnqueue()`.
- [x] Добавить тесты приема сообщений (`TryEnqueue` в `Running` / до старта / с `null`).
- [x] Добавить тесты порядка обработки сообщений и запрета параллельного исполнения (используя `TaskCompletionSource` и `WaitAsync`).

### Шаг 4: Остановка
- [x] Реализовать `StopAsync()` через `Writer.TryComplete()`.
- [x] Добавить тест, доказывающий, что принятые до `StopAsync` сообщения обрабатываются до конца.
- [x] Добавить тесты идемпотентности `StopAsync` и отклонения новых сообщений после остановки.
- [x] Добавить тест `StopAsync_BeforeStart_ChangesStateToStopped`.

### Шаг 5: Обработка паники
- [x] Добавить перехват исключений из handler, переход в `Faulted` и вызов `panicHandler`.
- [x] Добавить тесты:
  - `HandlerException_ChangesStateToFaulted` (состояние Faulted, вызов panic handler, запрет новых сообщений)
  - `HandlerException_StateRemainsFaultedAfterStopAsync`
  - `PanicHandlerException_DoesNotPreventLoopCompletion`

### Шаг 6: Очистка ресурсов
- [x] Реализовать `IAsyncDisposable` (`DisposeAsync` вызывает `await StopAsync()`).
- [x] Добавить тест `DisposeAsync_StopsRunningLoop`.

### Шаг 7: Финальная проверка
- [x] `dotnet build --configuration Release`
- [x] `dotnet test --configuration Release`
- [x] `git diff --check`
- [x] Проверить отсутствие лишних изменений.

## Результат проверки

- [x] Код компилируется без ошибок (`dotnet build --configuration Release` пройден).
- [x] Все тесты успешно пройдены (`dotnet test --configuration Release` показал 111 Passed).
- [x] Лишних изменений не внесено (новые файлы `EngineLoop.cs`, `EngineLoopState.cs`, `EngineMessage.cs`, `EngineLoopTests.cs` соответствуют задачам; мусора нет).
- [x] Остановки и аварийные перехваты полностью покрыты тестами.
