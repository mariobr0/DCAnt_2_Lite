## 1. План разработки

### Текущий статус

- [x]  Этап 1. Каркас проекта
- [x]  Этап 2. Базовые типы и округление
- [x]  Этап 3. DCA-математика
- [x]  Этап 4. Торговая модель
- [x]  Этап 5. EngineLoop
- [x]  Этап 6. SQLite и Outbox
- [x]  Этап 7. Quantower contract tests
- [x]  Этап 8. Минимальная защищенная сделка
- [x]  Этап 9. Полный DCA-цикл
- [ ]  Этап 10. Дополнительные функции

Текущий OpenSpec change: не выбран.

### Этап 1. Каркас проекта

Создать:

- solution;
- production-проекты;
- тестовые проекты;
- Project References;
- `Directory.Build.props`;
- `.editorconfig`;
- базовую Release-сборку.

Этап готов, когда:

- все проекты собираются;
- тесты запускаются;
- зависимости направлены правильно;
- торговая логика еще не добавлена.

Первый change:

`create-solution-foundation`

### Этап 2. Базовые типы и округление

Создать:

- `StrategyInstanceId`;
- `TradeCycleId`;
- `InternalOrderId`;
- `EffectId`;
- `Price`;
- `Quantity`;
- `Money`;
- `Currency`;
- `InstrumentRules`;
- правила округления.

Этап готов, когда:

- типы immutable;
- разные ID нельзя случайно смешать;
- некорректные значения отклоняются;
- вычисления используют `decimal`;
- округление проверено тестами.

Changes:

- `create-core-value-objects`;
- `implement-instrument-rounding`.

### Этап 3. DCA-математика

Создать:

- `GridSettings`;
- `GridLevel`;
- `GridPlan`;
- `GridCalculator`;
- step scaling;
- volume scaling;
- проверку капитала.

Этап готов, когда:

- цены монотонны;
- уровни уникальны после округления;
- капитал не превышается;
- minimum notional соблюдается;
- delay levels работают по заданным правилам.

Change:

`implement-grid-calculation`

### Этап 4. Торговая модель

Создать:

- `TradeCycle`;
- `ManagedOrder`;
- `ExecutionRecord`;
- Execution Ledger;
- расчет quantity;
- расчет VWAP;
- базовые состояния;
- `OperatingMode`.

Этап готов, когда:

- повторный Execution не учитывается дважды;
- поздний Execution не отбрасывается;
- ордер принадлежит одному `TradeCycle`;
- новый цикл не начинается до завершения предыдущего;
- `DcaPaused` и `ExitOnly` запрещают увеличение позиции.

Changes:

- `create-order-and-execution-model`;
- `implement-trade-cycle`.

### Этап 5. EngineLoop

Создать:

- `EngineMessage`;
- Channel;
- одного consumer;
- последовательную обработку;
- сообщения timer;
- controlled shutdown;
- quote coalescing.

Этап готов, когда:

- только `EngineLoop` изменяет состояние;
- одинаковые сообщения дают одинаковый результат;
- shutdown не создает новые торговые действия;
- надежные торговые события не удаляются.

Change:

`implement-engine-loop`

### Этап 6. SQLite и Outbox

Подключить:

- `Microsoft.Data.Sqlite`;
- Dapper;
- `DatabaseMigrationRunner`;
- `ITradingStateStore`;
- `TradeCycles`;
- `ManagedOrders`;
- `Executions`;
- `Outbox`;
- `SchemaMigrations`.

Этап готов, когда:

- эффект сохраняется до отправки;
- транзакционно связанные изменения атомарны;
- состояние загружается после restart;
- неизвестный effect не повторяется вслепую;
- ошибка записи запрещает увеличение позиции.

Change: 

`implement-sqlite-state-store`

### Этап 7. Quantower Contract Tests

Проверить фактическое поведение Quantower API и выбранного broker connector.
На этом этапе не реализуется полный DCA-цикл.
Этап готов, когда критические предположения об API подтверждены или явно помечены как неподдерживаемые.

Change: 

`investigate-quantower-contracts`

- Проверить, обеспечивает ли ClientRequestId фактическую защиту от повторной отправки на выбранном connector.
- Проверить возможность поиска ордера по ClientRequestId.
- Проверить срок и область уникальности ClientRequestId.
- Проверить фактическое поведение rate limits и timeout.
- Определить, какие read-only запросы допускают автоматический retry.

### Этап 8. Минимальная защищенная сделка

Реализовать следующий сценарий:

Market Entry → Execution → Position Active → Take Profit или Stop Loss → Exit Execution → TradeCycle Completed.

Обязательные сценарии:

- partial entry;
- entry rejection;
- entry timeout;
- безопасная замена TP;
- безопасная замена SL;
- cancel/fill race;
- restart;
- reconnect;
- late Execution;
- duplicate events.

На этом этапе нет полной DCA-сетки.

Change:

`implement-protected-single-trade`

### Этап 9. Полный DCA-цикл

Последовательность:

1. Один DCA-level.
2. Partial fill уровня.
3. Пересчет VWAP.
4. Обновление TP и SL.
5. Restart с активной сеткой.
6. Несколько DCA-уровней.
7. Active grid window.
8. Grid cooldown.
9. Step scaling.
10. Volume scaling.
11. `DcaPaused`.
12. `ExitOnly`.

Этап готов, когда:

- нет дублирующих entry, grid, TP и SL;
- исполненный уровень не создается повторно;
- capital limit не превышается;
- ошибка сетки не отключает TP и SL;
- `ExitOnly` не увеличивает позицию;
- recovery не повторяет неизвестный ордер.

Changes:

- `implement-one-grid-level`;
- `implement-multi-level-grid`;
- `implement-dca-pause`;
- `implement-exit-only`.

### Этап 10. Дополнительные функции

Добавлять только после стабилизации полного DCA-цикла.

Предлагаемый порядок:

1. Static Take Profit.
2. Dynamic NATR Take Profit (включая Take-Profit Recalc Mode).
3. Initial Entry Drop Stop.
4. Max PnL Stop.
5. Last Level Stop.
6. Time Stagnation.
7. Rescue Mode (включая Target Exit и Stop Bot After Recovery).
8. Smart Entry EMA (включая Dynamic NATR Drop).
9. Smart Entry MFI.
10. Global Trend Filter.
11. Auto Reinvest.
12. Short direction.
13. Startup Policy (Adopt, Refuse, Flatten).
14. Time Between Grid Orders (Grid Cooldown Timeout).
15. Dynamic NATR Grid Step.
16. Step Scale Delay.
17. Volume Scale Delay.
18. Stop Bot After Stop-Loss.
19. Averaging Table Calculation (внутренний сервис для UI).

Каждая функция реализуется отдельным `change.md`.

Short добавляется только после подтвержденной стабильности Long.

---

## 2. Тестирование

### Unit tests

Проверяют:

- value objects;
- округление;
- DCA-математику;
- VWAP;
- комиссии;
- TP;
- SL;
- конфигурационную валидацию.

### Transition tests

Используют модель:

`State + Event → New State + Effects`

### Property-based tests

Проверяют множество автоматически созданных входных значений.

Основные свойства:

- цены положительны;
- сетка монотонна;
- капитал не превышается;
- округленные уровни уникальны;
- повторный Execution идемпотентен;
- `ExitOnly` не увеличивает позицию;
- один `TradeCycle` не создает два логических TP.

### Integration tests

Проверяют:

- SQLite;
- миграции;
- Outbox;
- транзакции;
- restart recovery;
- ошибки записи.

### Scenario tests

Проверяют:

- обычную сделку;
- partial fills;
- rejection;
- timeout;
- TP;
- SL;
- reconnect;
- restart;
- late events;
- duplicate events.

### Quantower tests

Запускаются отдельно на демо-счете.

Markdown-описание сценария не заменяет автоматический тест.

---

## 3. Минимальный Definition of Done

Изменение считается завершенным, если:

1. Реализация соответствует разделу `Требуемое поведение` в `change.md`.
2. Не реализована функциональность за пределами утвержденного scope.
3. Проект собирается в Release.
4. Применимые автоматические тесты проходят.
5. Важные исключения не подавляются без записи ошибки.
6. Новое поведение не создает дублирующие торговые действия.
7. Для внешнего действия определено поведение при timeout.
8. Для order lifecycle проверены duplicate и partial fill, если они применимы.
9. Для сохраняемого состояния определено поведение после restart.
10. Чекбоксы отражают фактическое состояние работы.
11. Результат проверен перед архивированием.
12. Не добавлена лишняя архитектурная сложность.

Не каждое изменение требует всех видов тестирования.

Например, исправление текста сообщения не требует recovery-теста.

## 4. Что отложено

До завершения базового MVP откладываются:

- Short;
- одновременные Long и Short;
- Hedge Mode;
- Netting Mode;
- dynamic ranges;
- portfolio supervisor;
- multi-account;
- multi-symbol runtime;
- funding allocation;
- multi-currency conversion;
- машинное обучение;
- walk-forward framework;
- Monte Carlo framework;
- автоматическое A/B-тестирование;
- сложные dashboards;
- автоматические UI-тесты;
- создание визуального Web-симулятора сетки для версии 2 (React, Vite, Node.js);
- эксперимент с заменой "Cancel -> Place" на "ModifyOrder".

Отложенная функция не должна усложнять текущую архитектуру.