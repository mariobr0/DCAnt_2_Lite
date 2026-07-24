---
description: Сохраняет и документирует один ручной Quantower contract test
---

# Collect Quantower Contract Test

Обработай один указанный пользователем завершенный ручной тест Quantower.

Не изменяй production-код.

## Входные данные

Используй предоставленные:

- сырой лог или путь к нему;
- Scenario;
- Quantower version;
- Connector;
- Account mode;
- Symbol;
- Git commit.

Неизвестные значения записывай как `Unknown`. Ничего не угадывай.

## 1. Проверка evidence

Проверь:

- один ли RunId используется;
- согласованы ли Scenario и TargetMarker;
- совпадают ли AccountId и SymbolId;
- используются ли UTC timestamps;
- не смешаны ли чужие ордера;
- известны ли финальные состояния ордера и позиции;
- нет ли ошибки диагностической стратегии.

Отделяй поведение Quantower от дефектов диагностики.

Если тест испорчен, результат: `MustBeRepeated`.

## 2. Хронология

Выпиши по UTC:

- StartSnapshot;
- BeforeApiCall;
- API result;
- AfterApiCall;
- OrderAdded или другие callbacks;
- OrdersHistoryAdded;
- probes;
- timeout;
- OnStop.

Рассчитай реальные интервалы между событиями.

Не превращай наблюдаемую задержку в production timeout.

## 3. Корреляция

Сравни доступные:

- TargetMarker и Comment;
- ReturnedOrderId;
- OrderAdded OrderId;
- OrdersHistoryAdded OrderId;
- Core.Orders OrderId;
- AccountId и SymbolId.

Цена и quantity сами по себе не доказывают ownership.

`OrdersHistoryAdded` не считать Execution.

## 4. Результат

Выбери один результат:

- `Passed`
- `PassedWithLimitations`
- `Failed`
- `MustBeRepeated`

Оцени evidence:

- `High`
- `Medium`
- `Low`
- `Invalid`

Укажи:

- final order state;
- final position state;
- matching active orders;
- ManualCleanupRequired.

При неизвестном состоянии не рекомендовать следующий торговый тест.

## 5. Сохранение raw log

Сохрани неизмененный лог в:

`docs/quantower-contracts/evidence/YYYY-MM-DD_<scenario>_<run-id>.log`

Перед логом добавь только metadata:

- Scenario;
- RunId;
- TargetMarker;
- Quantower version;
- Connector;
- Account mode;
- Symbol;
- Git commit;
- Result;
- Evidence quality.

Не исправляй строки raw log и не перезаписывай конфликтующий файл.

## 6. Отчет запуска

Создай:

`docs/quantower-contracts/runs/YYYY-MM-DD_<scenario>_<run-id>.md`

Разделы:

- Environment
- Validation
- Timeline
- ID and Comment correlation
- Confirmed observations
- Unknowns
- Diagnostic issues
- Final state
- Supported contracts
- Result and next action

### Требования к формулировкам в отчете (Wording Requirements)
1. **Категоричность**: Запрещено писать "event fires reliably" на основе одного теста. Используй: "In this test, [Event] was observed after [Action]".
2. **Тайминги**: Запрещено использовать слово "exactly". Пиши: "[Event] was observed approximately X ms after the [Action] API result in this run". Замеряй время от строки API result, а не от AfterApiCall.
3. **Синхронность**: Запрещено писать "PlaceOrder executes synchronously". Пиши: "The PlaceOrder method returned synchronously with local status Success and a populated ReturnedOrderId. The order became observable asynchronously afterward."
4. **OrdersHistoryAdded**: Всегда отмечай его первое появление: "`OrdersHistoryAdded` was observed with `Status=Opened` and `FilledQty=0`. Therefore, the event name does not imply that an execution occurred."
5. **Дефекты**: Вместо "Diagnostic issues: None" пиши: "No diagnostic defects affecting this run were observed."
6. **Environment**: Разделяй `Symbol name` и `Symbol ID` (если имя неизвестно, пиши Unknown). Для Account добавляй примечание, если Id и Name одинаковые: "В данном connector наблюдаемые Account.Id и Account.Name были одинаковыми либо диагностическая стратегия вывела одинаковые значения. Это не исследовалось отдельно."

## 7. Обновление контрактов

Обнови:

`docs/quantower-contracts/single-order-contracts.md`

Используй только существующие Contract IDs.

Статусы:

- `Confirmed` (всегда добавляй уточнение, например: `Confirmed for the tested environment`)
- `Rejected`
- `Inconclusive`
- `NotSupported`
- `NotTested`

Для каждого обновленного контракта строго используй следующую структуру и правила:

- **Status**: с уточнением (см. выше).
- **Observation**: точные наблюдаемые факты (без обобщений).
- **Conclusion**: вывод только для этого конкретного сценария.
- **Limitations**: всегда перечисляй, что НЕ проверено (Cancel, Modify, reconnect, restart, load, other connectors, etc.).
- **Architectural consequence**: строгие последствия для архитектуры (например, "Do not use fixed delays", "Store local API result separately", "Full lifecycle must be verified").
- **References**: ссылки на raw log и run report.

**Строгие запреты для контрактов:**
1. Запрещено рекомендовать фиксированные задержки (polling delays) или `Thread.Sleep`. Наблюдаемое время задержки не является гарантией.
2. Запрещено делать глобальные выводы на основе одного сценария. Пиши "Confirmed for this run" или "Confirmed for the tested Place scenario".
3. Не пиши, что метод "executes synchronously". Пиши, что он "returned local status Success synchronously. The order became observable asynchronously afterward."
4. Не пиши "can be safely used". Указывай, что это "candidate for correlation", так как надежность нужно проверять для каждого lifecycle event (Cancel, Filled и т.д.).

Если новое evidence противоречит старому, сохранить оба и поставить
`Inconclusive`.

## 8. Обновление индекса

Обнови:

`docs/quantower-contracts/README.md`

Добавь Scenario, RunId, Result, EvidenceQuality, Contract IDs и ссылки
на evidence и run report. Не дублируй существующий RunId.

## 9. Ограничения

Разрешено менять только `docs/quantower-contracts/`.

Не изменяй:

- `src/`;
- диагностическую Strategy;
- EngineLoop;
- TradeCycle;
- QuantowerAdapter;
- SQLite;
- Outbox.

Не создавай ExecutionId и не проектируй production-логику.

## 10. Финальная проверка

Выполни:

- `git status --short`;
- `git diff --check`;
- проверку ссылок и отсутствия дубликатов RunId.

## Итоговый ответ

Сообщи:

- Scenario, RunId и Result;
- EvidenceQuality;
- final order and position state;
- ManualCleanupRequired;
- обновленные Contract IDs;
- созданные файлы;
- обнаруженные проблемы;
- следующее действие:

`ReadyForNextScenario`,
`ManualCleanupRequired`,
`RepeatCurrentTest` или
`ReviewConflictingEvidence`.

Не создавай commit и не архивируй change.